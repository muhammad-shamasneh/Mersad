# Mersad V1 — Alert Architecture

## 1. Purpose

The Alert layer converts security detections into persistent, user-facing security findings.

The distinction between telemetry, detection, and alert is intentional:

```text
Telemetry
    ↓
Detection
    ↓
Alert
```

* **Telemetry** is raw evidence collected from an endpoint.
* **Detection** is security logic that determines whether the evidence matches suspicious behavior.
* **Alert** is a persistent security finding that can be displayed, investigated, and managed by a user.

---

## 2. Alert Flow

The current V1 telemetry pipeline is:

```text
Windows Agent
     ↓
Telemetry API
     ↓
Validate telemetry
     ↓
Persist telemetry
     ↓
Detection Engine
     ↓
Detection objects
     ↓
AlertService
     ↓
Alerts table
```

Detection currently runs synchronously after telemetry has been persisted.

---

## 3. Alert Model

The V1 `Alert` model contains:

```text
Alert
├── Id
├── DeviceId
├── DetectionRuleId
├── Title
├── Description
├── Severity
├── Status
├── CreatedAt
├── UpdatedAt
├── ProcessEventId?
└── NetworkEventId?
```

### Id

Unique identifier for the alert.

### DeviceId

Identifies the device where the suspicious activity occurred.

### DetectionRuleId

Identifies the detection rule that generated the alert.

Examples:

```text
PROC-001
NET-001
CORR-001
```

### Title

Short, user-facing description of the security finding.

### Description

Provides additional context about why the activity was considered suspicious.

### Severity

Represents the severity assigned by the detection rule.

Current V1 values include:

```text
Medium
High
```

### Status

Represents the lifecycle state of the alert.

V1 starts every alert as:

```text
New
```

The intended lifecycle is:

```text
New → Acknowledged → Resolved
```

### CreatedAt / UpdatedAt

Track the alert lifecycle timestamps.

### ProcessEventId / NetworkEventId

These optional fields link the alert back to the raw telemetry that caused the detection.

This allows an investigation workflow to navigate from:

```text
Alert
  ↓
Detection
  ↓
Evidence
  ↓
Process / Network telemetry
```

---

## 4. Why Alerts Store Evidence References

An Alert should not duplicate all of the telemetry that caused it.

For example, `PROC-001` detects a PowerShell process.

The alert stores:

```text
ProcessEventId
```

instead of copying all process-event fields into the alert.

Similarly, `NET-001` stores:

```text
NetworkEventId
```

and `CORR-001` stores both:

```text
ProcessEventId
NetworkEventId
```

This keeps the alert focused on the security finding while preserving a link to the original evidence.

---

## 5. Detection vs Alert

Detection and Alert are deliberately separate concepts.

### Detection

A detection represents the result of security analysis.

Example:

```text
Rule: PROC-001
PowerShell execution detected.
```

Detection objects are currently produced in memory by the detection engine.

### Alert

An alert represents a persistent security finding.

Example:

```text
Title: Suspicious PowerShell Execution
Severity: Medium
Status: New
```

The alert can later be:

* displayed in the dashboard
* acknowledged
* resolved
* investigated
* explained by AI

This separation allows the detection engine to remain focused on security logic while the alert layer handles persistence and user-facing lifecycle management.

---

## 6. AlertService

`AlertService` converts detections into persistent `Alert` records.

The service:

1. Receives detections from the detection engine.
2. Skips duplicate detection keys within the current evaluation.
3. Checks whether an alert already exists for the same rule and evidence references.
4. Creates alerts only when a matching record is not found.
5. Persists newly created alerts.

The deduplication key consists of the detection rule ID, process event ID, and network event ID.

This is application-level deduplication. Concurrent requests can still race between checking for an existing alert and inserting a new one. A database uniqueness constraint or equivalent concurrency-safe mechanism is needed for stronger guarantees.

Detection runs synchronously as part of telemetry request processing; a background alert-processing queue is not currently implemented.
---

## 7. Current Detection-to-Alert Example

For the V1 test scenario:

```text
Process:
PID 7000
powershell.exe

Network:
PID 7000
Remote Port 1337
```

The detection engine produced:

```text
PROC-001
NET-001
CORR-001
```

The resulting database records were:

```text
PROC-001
Severity: Medium
Status: New
ProcessEventId: populated
NetworkEventId: null
```

```text
NET-001
Severity: High
Status: New
ProcessEventId: null
NetworkEventId: populated
```

```text
CORR-001
Severity: High
Status: New
ProcessEventId: populated
NetworkEventId: populated
```

This confirms that alerts retain references to the evidence used by their corresponding detections.

---

## 8. Current Alert Lifecycle

V1 initializes every new alert with:

```text
New
```

The intended lifecycle is:

```text
New
  ↓
Acknowledged
  ↓
Resolved
```

The status transition API and dashboard controls are not implemented yet.

---

## 9. Current Architecture

The current architecture is:

```text
┌──────────────────┐
│  Windows Agent   │
└────────┬─────────┘
         │
         │ Telemetry
         ▼
┌──────────────────┐
│ Telemetry API    │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   PostgreSQL     │
│ Process / Network│
│     Events       │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ Detection Engine │
└────────┬─────────┘
         │
         │ Detections
         ▼
┌──────────────────┐
│   AlertService   │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│    Alerts DB     │
└──────────────────┘
```

The dashboard will consume the `Alerts` data through an API in the next stage.

---

## 10. V1 Limitations

The current implementation provides alert persistence, an alerts API, and a dashboard alert feed. `AlertService` performs application-level deduplication using the detection rule ID and associated process/network event references.

Remaining limitations include:

* Deduplication is not concurrency-safe; simultaneous requests can create duplicate alerts.
* Alert grouping and suppression are not implemented.
* Alert status update workflows and dashboard status controls are not implemented.
* Alert ownership, comments, and multi-user workflows are not implemented.
* Authentication, authorization, notification channels, and background alert processing are not verified as implemented.

The deduplication check should not be treated as a database-enforced uniqueness guarantee.
---

## 11. Future Direction

Future versions can evolve the alert layer into a complete security investigation workflow:

```text
Telemetry
    ↓
Detection
    ↓
Alert
    ↓
Investigation
    ├── Process evidence
    ├── Network evidence
    ├── Related events
    └── Timeline
    ↓
AI Explanation
    ↓
Response / Action
```

Potential future improvements include:

* alert deduplication and grouping
* historical correlation
* configurable severity
* customer-specific detection policies
* alert assignment
* notification integrations
* automated response actions
* richer investigation timelines

---

## 12. V1 Summary

The Alert layer provides the bridge between Mersad's detection engine and its future security dashboard.

The current implementation successfully supports:

* persistent alerts
* detection rule identification
* severity
* alert lifecycle status
* device association
* process evidence references
* network evidence references
* synchronous detection-to-alert processing

The next component is the **Alert API**, which will expose these persistent security findings to the Mersad dashboard.
