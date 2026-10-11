# Mersad V1 — Detection

## 1. Purpose

The detection system is responsible for analyzing ingested telemetry and identifying behavior that matches predefined suspicious patterns.

The detection layer sits between telemetry ingestion and alerting:

```text
Agent
  ↓
Telemetry
  ↓
Backend
  ↓
Database
  ↓
Detection Engine
  ↓
Detection
  ↓
Persistent Alert
```

Telemetry describes **what happened**.

Detection describes **why an event or combination of events is considered suspicious**.

An alert represents a persistent security finding generated from a detection and exposed through the alerts API for presentation in the dashboard. Investigation workflows and alert status management are separate capabilities and are not currently implemented in the verified V1 flow.

---

## 2. Detection Architecture

The detection system consists of four main concepts:

```text
DetectionContext
       ↓
DetectionEngine
       ↓
IDetectionRule
       ↓
Detection
```

### DetectionContext

Provides the telemetry that rules are allowed to inspect.

### DetectionEngine

Coordinates all registered detection rules.

### IDetectionRule

Defines the common contract that every detection rule must implement.

### Detection

Represents the result produced when a rule identifies suspicious behavior.

---

## 3. DetectionContext

`DetectionContext` provides a consistent view of the telemetry being evaluated.

It currently contains:

* Process events
* Network events

Conceptually:

```text
DetectionContext
├── ProcessEvents
└── NetworkEvents
```

The context uses `IReadOnlyCollection<T>` because detection rules should inspect telemetry rather than modify it.

The context also prevents individual rules from directly querying the database.

This keeps the detection layer separated from persistence.

### Current V1 behavior

The context contains the events from the **current telemetry ingestion batch**.

For example:

```text
Telemetry Batch
├── ProcessEvent
├── ProcessEvent
├── NetworkEvent
└── NetworkEvent

        ↓

DetectionContext
```

This is intentionally simple for V1.

---

## 4. IDetectionRule

Every detection rule implements the `IDetectionRule` interface.

The interface defines:

```text
RuleId
Evaluate(DetectionContext)
```

A rule can produce:

* zero detections
* one detection
* multiple detections

Therefore `Evaluate()` returns `IEnumerable<Detection>`.

Conceptually:

```text
Detection Rule
      ↓
Evaluate(context)
      ↓
0..N Detection objects
```

This allows new detection rules to be added without modifying the detection engine itself.

---

## 5. DetectionEngine

The `DetectionEngine` receives all registered `IDetectionRule` implementations through dependency injection.

Conceptually:

```text
                    ┌── PROC-001
                    │
DetectionEngine ────┼── NET-001
                    │
                    └── CORR-001
```

The engine evaluates every registered rule against the same `DetectionContext`.

Adding a new rule therefore requires:

1. Implementing `IDetectionRule`
2. Registering the rule with dependency injection

The engine itself does not need to be modified.

This follows an important separation of responsibilities:

```text
Engine → coordinates rules

Rule → contains detection logic
```

---

## 6. Detection Model

A detection contains:

* `RuleId`
* `Title`
* `Description`
* `Severity`
* `DeviceId`
* `ProcessEventId`
* `NetworkEventId`
* `DetectedAt`

The event references are important because a detection should be traceable back to the telemetry that caused it.

For example:

```text
Detection
├── RuleId: CORR-001
├── Severity: High
├── ProcessEventId: ...
└── NetworkEventId: ...
```

This creates an evidence relationship:

```text
Detection
   │
   ├── Process Event
   │
   └── Network Event
```

The evidence references are nullable because not every detection requires multiple event types.

For example:

```text
Process-only detection
    → ProcessEventId

Network-only detection
    → NetworkEventId

Correlated detection
    → ProcessEventId + NetworkEventId
```

---

# 7. V1 Detection Rules

Mersad V1 currently contains three detection rules:

```text
PROC-001
NET-001
CORR-001
```

---

## 7.1 PROC-001 — Suspicious PowerShell Execution

### Purpose

Detect execution of:

```text
powershell.exe
```

The rule performs a case-insensitive process-name comparison.

### Logic

```text
For each ProcessEvent:

    Is ProcessName == powershell.exe?
        │
        ├── No → Ignore
        │
        └── Yes → Create Detection
```

Example:

```text
ProcessEvent
PID: 7000
Name: powershell.exe
```

Produces:

```text
PROC-001
Suspicious PowerShell Execution
Severity: Medium
```

### Important distinction

This rule does **not** claim that PowerShell is malicious.

PowerShell is a legitimate Windows administration tool.

The rule identifies PowerShell execution as behavior that may be interesting from a security perspective.

It is therefore a heuristic rather than proof of malicious activity.

---

# 8. NET-001 — Suspicious Network Connection

### Purpose

Detect connections to ports currently considered suspicious by Mersad's network detection policy.

The current V1 suspicious ports are:

```text
4444
1337
31337
```

### Logic

```text
For each NetworkEvent:

    Is RemotePort in suspicious-port policy?
        │
        ├── No → Ignore
        │
        └── Yes → Create Detection
```

Example:

```text
NetworkEvent
PID: 7000
RemotePort: 1337
```

Produces:

```text
NET-001
Suspicious Network Connection
Severity: High
```

---

# 9. Configurable Detection Policy

Mersad's backend loads detection settings through the .NET configuration system and binds them to the `DetectionOptions` class.

**Configuration source:** `backend/Mersad.Api/appsettings.json`

The `Detection` section defines three independent rule flags and the suspicious remote ports:

```json
"Detection": {
  "SuspiciousPowerShellEnabled": true,
  "SuspiciousNetworkPortEnabled": true,
  "PowerShellSuspiciousNetworkEnabled": true,
  "SuspiciousNetworkPorts": [
    4444,
    1337,
    31337
  ]
}
```

These values represent the current backend defaults. Environment-specific configuration can override settings through the .NET configuration providers when configured appropriately.

## Configuration responsibilities

* `SuspiciousPowerShellEnabled` controls rule `PROC-001`.
* `SuspiciousNetworkPortEnabled` controls rule `NET-001`.
* `PowerShellSuspiciousNetworkEnabled` controls rule `CORR-001`.
* `SuspiciousNetworkPorts` defines the remote ports matched by `NET-001` and `CORR-001`.

The network rules use the same configured port list, avoiding separate hardcoded lists in each rule.

## Validation

`DetectionOptions` validates that configured ports are within the valid TCP/UDP port-number range of 1–65535 and that the list contains no duplicates. The API validates these options at startup and fails to start if validation fails.

An empty port list is valid and means that no remote port matches this particular port-based policy.

## Current limitations

* These settings currently configure backend detection behavior only.
* Configuration changes are not dynamically reloaded by the detection rules; restart the API to apply changes.
* The agent and dashboard do not yet share a complete centralized configuration system.
* These port matches are heuristic signals, not proof of malicious activity.


### V1 design decision

The policy is currently defined in code.

This is intentional for V1.

In V2, configurable detection policies should move into the centralized configuration system so different customers can have different detection settings.

---

# 10. CORR-001 — PowerShell Suspicious Network Activity

`CORR-001` is the first correlation rule in Mersad.

Unlike the previous rules, it does not simply look at one event.

It establishes a relationship between two events.

The rule requires:

1. A PowerShell process
2. A network event
3. The same process ID
4. A suspicious remote port

Conceptually:

```text
ProcessEvent
    │
    │ ProcessName = powershell.exe
    │
    │ ProcessId = 7000
    │
    ▼
NetworkEvent
    │
    │ ProcessId = 7000
    │
    │ RemotePort = suspicious
    ▼
CORR-001
```

### Example

Process:

```text
PID: 7000
Name: powershell.exe
```

Network connection:

```text
PID: 7000
RemotePort: 1337
```

Both events refer to PID `7000`.

Therefore:

```text
CORR-001
PowerShell Suspicious Network Activity
Severity: High
```

The detection references both events:

```text
ProcessEventId
NetworkEventId
```

This makes the detection explainable and traceable to its evidence.

---

# 11. Why Process ID Matters

Consider:

```text
Process:
PID 7000
powershell.exe

Network:
PID 8000
Port 1337
```

The two events contain suspicious characteristics, but they do not establish that PowerShell made the network connection.

Therefore:

```text
PROC-001 → Yes
NET-001  → Yes
CORR-001 → No
```

The process ID is what establishes the relationship between the process and network activity.

---

# 12. Detection Execution Flow

Detection runs synchronously during telemetry ingestion, after the telemetry events have been persisted.

The V1 flow is:

```text
Agent
  ↓
POST /api/telemetry
  ↓
Validate and process telemetry
  ↓
Persist telemetry
  ↓
Create DetectionContext
  ↓
DetectionEngine.Evaluate()
  ↓
Run registered detection rules
  ↓
Generate detections
  ↓
AlertService.CreateAlertsAsync()
  ↓
Persist alerts
  ↓
Return HTTP response
```

The `TelemetryController` coordinates the process. The `DetectionEngine` evaluates the registered rules against the current ingestion batch, and `AlertService` converts each detection into a persistent `Alert` record.

Each alert retains relevant information from its detection, including the device ID, rule ID, title, description, severity, and references to the associated process or network events.

New alerts are initially assigned the status `New`.

The dashboard retrieves persisted alerts through `GET /api/alerts`. This separates telemetry ingestion and detection from the presentation of security findings.

Alert status management, deduplication, and historical correlation are separate capabilities and are not provided by this flow.

# 13. Why Detection Runs After Persistence

The current order is:

```text
Validate
   ↓
Persist
   ↓
Detect
```

rather than:

```text
Validate
   ↓
Detect
   ↓
Persist
```

This ensures the telemetry that produced a detection already exists in the database.

That is important because detections contain references to their evidence events.

For example:

```text
Detection
    │
    ├── ProcessEventId ──→ ProcessEvents table
    │
    └── NetworkEventId ──→ NetworkEvents table
```

---

# 14. Current V1 Correlation Limitation

`CORR-001` currently evaluates events contained in the **same ingestion batch**.

For example:

### Batch A

```text
ProcessEvent
PID: 7000
powershell.exe
```

### Batch B

```text
NetworkEvent
PID: 7000
RemotePort: 1337
```

The current V1 engine will not correlate these two events because they are evaluated in separate `DetectionContext` instances.

Therefore:

```text
Batch A → no CORR-001
Batch B → no CORR-001
```

even though the events could logically belong together.

This is a known V1 limitation.

A future detection architecture can support historical correlation by querying recent telemetry or maintaining a detection state/window.

---

# 15. Dependency Injection

Detection rules are registered with ASP.NET Core dependency injection.

The current rules are:

```text
SuspiciousPowerShellRule
SuspiciousNetworkPortRule
PowerShellSuspiciousNetworkRule
```

The application registers them as implementations of:

```text
IDetectionRule
```

The `DetectionEngine` receives:

```text
IEnumerable<IDetectionRule>
```

This means the engine automatically receives all registered rules.

The benefit is that the engine does not need to know the concrete rule types.

---

# 16. V1 Detection Design

The resulting architecture is:

```text
                 Telemetry
                    │
                    ▼
            DetectionContext
                    │
                    ▼
            DetectionEngine
                    │
          ┌─────────┼─────────┐
          ▼         ▼         ▼
      PROC-001   NET-001   CORR-001
          │         │         │
          └─────────┼─────────┘
                    ▼
               Detection
```

This gives Mersad a basic extensible detection architecture without introducing unnecessary infrastructure during the MVP phase.

---

# 17. What Is Not Implemented Yet

The following capabilities are not part of the currently verified V1 implementation:

* Alert status management
* Alert deduplication
* Alert aggregation
* Historical correlation across separate ingestion batches
* Detection state and time-window management
* Configurable rules
* Customer-specific detection policies
* Sigma rule support
* Background detection workers
* Detection queues
* Advanced behavioral analytics
* Machine-learning-based detection
* Investigation workflows

The current V1 implementation already includes detection rules, persistent alert storage, an alerts API, and a dashboard alert feed.

Future capabilities should be added incrementally based on product requirements rather than introducing unnecessary infrastructure during the MVP phase.


---

# 18. Future Direction

The V1 detection architecture is intended to evolve toward:

```text
Telemetry
    ↓
Detection Pipeline
    ↓
Rules / Correlation
    ↓
Detection
    ↓
Alert
    ↓
Investigation
```

For V2, configuration should become centralized:

```text
Configuration
      ↓
Configuration Engine
      ↓
Typed Configuration
      ↓
Detection Rules
```

This will eventually allow customer-specific detection policies without changing the agent or backend code.

Historical correlation can also evolve from the current batch-only model into a time-window-based detection system.

---

## 19. V1 Summary

Mersad V1 now has a functional detection foundation capable of:

* Detecting PowerShell execution
* Detecting connections to suspicious ports
* Correlating PowerShell processes with suspicious network activity
* Referencing the telemetry that caused a detection
* Supporting multiple detection rules through a common interface
* Sharing detection policies across rules
* Adding new rules without modifying the detection engine

The current system favors simplicity and clear architecture so that more advanced detection capabilities can be added later without redesigning the entire telemetry pipeline.
