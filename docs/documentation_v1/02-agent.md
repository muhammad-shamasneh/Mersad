# Mersad V1 — Agent

## 1. Overview

The Mersad Agent is a Windows-native C++20 application responsible for collecting endpoint telemetry and sending it to the Mersad backend.

The Agent is intentionally kept focused on three primary responsibilities:

1. Identify the Agent installation.
2. Collect endpoint telemetry.
3. Transport telemetry to the backend.

The Agent does not own database logic, detection logic, alert generation, or dashboard functionality.

---

## 2. Agent Structure

The current Agent is organized around several components:

```text
Mersad.Agent
│
├── AgentIdentity
│   └── Persistent AgentId
│
├── DeviceManager
│   └── Device registration
│
├── ProcessCollector
│   └── Process discovery
│
├── NetworkCollector
│   └── IPv4 TCP connection discovery
│
├── TelemetryClient
│   └── Telemetry batching and JSON construction
│
└── HttpClient
    └── HTTP communication
```

Each component has a specific responsibility.

---

## 3. Startup Flow

The current Agent startup sequence is:

```text
Agent starts
    │
    ▼
Print startup information
    │
    ▼
Get hostname
    │
    ▼
Load or create AgentId
    │
    ▼
Register device
    │
    ▼
Collect processes
    │
    ▼
Send process telemetry
    │
    ▼
Collect network connections
    │
    ▼
Send network telemetry
    │
    ▼
Exit
```

The current V1 Agent is a simple executable rather than a continuously running Windows service.

A production version would eventually run continuously and collect telemetry periodically.

---

## 4. Agent Identity

The Agent uses a persistent GUID called `AgentId`.

The purpose is to provide a stable identity across Agent restarts.

The Agent follows this logic:

```text
Mersad.AgentId exists?
        │
        ├── Yes
        │    ↓
        │  Load it
        │
        └── No
             ↓
          Generate GUID
             ↓
          Save GUID
```

The identity is stored in:

```text
Mersad.AgentId
```

For V1, this is a simple local file.

### Why persistence matters

Without persistence, the Agent could generate a new identity every time it starts.

For example:

```text
Start 1 → AgentId A
Start 2 → AgentId B
Start 3 → AgentId C
```

The backend would interpret these as different Agent installations.

With persistent identity:

```text
Start 1 → AgentId A
Restart → AgentId A
Restart → AgentId A
```

The backend can therefore associate the Agent with the same Device record.

---

## 5. AgentId Is Not Authentication

The current `AgentId` should be understood as an identifier, not a secure authentication mechanism.

The Agent sends:

```text
AgentId = X
```

The backend trusts this identifier for V1 device resolution.

However, the Agent does not currently prove cryptographically that it is the legitimate owner of that identity.

For example, if someone copies:

```text
Mersad.AgentId
```

to another machine, the second machine could present the same AgentId.

This could cause telemetry from two physical machines to be attributed to the same backend Device.

This is a known V1 security limitation.

---

## 6. Device Registration

After obtaining its identity, the Agent registers itself.

The Agent sends information similar to:

```json
{
  "agentId": "GUID",
  "hostname": "DESKTOP-EXAMPLE",
  "os": "Windows",
  "osVersion": "11"
}
```

The request is sent to:

```text
POST /api/devices
```

The backend checks whether the `AgentId` already exists.

### Existing Agent

If the Agent already exists:

```text
AgentId
   ↓
Existing Device
   ↓
Update device information
   ↓
Return existing Device.Id
```

### New Agent

If the Agent is new:

```text
AgentId
   ↓
Create Device
   ↓
Generate Device.Id
   ↓
Return Device.Id
```

The Agent extracts the returned `Device.Id` from the response.

---

## 7. DeviceManager

`DeviceManager` owns the device-registration responsibility.

It does not implement HTTP itself.

Instead:

```text
DeviceManager
      │
      │ registration JSON
      ▼
HttpClient
      │
      ▼
Mersad API
```

This keeps device-management logic separate from transport logic.

`DeviceManager` knows:

* What information must be registered.
* Which API endpoint to call.
* How to interpret the registration response.

`HttpClient` knows:

* How to perform an HTTP request.

---

## 8. Process Collection

`ProcessCollector` is responsible for discovering running Windows processes.

It uses Windows Tool Help APIs to enumerate processes.

The resulting internal representation is:

```cpp
struct ProcessInfo
{
    DWORD processId;
    std::string processName;
};
```

For each process, the Agent currently collects:

* Process ID
* Process name

Example:

```text
ProcessId: 1234
ProcessName: powershell.exe
```

The collector converts Windows API information into Mersad's internal `ProcessInfo` structure.

---

## 9. Source Validation

The Agent validates information as close to the source as practical.

For example, PID `0` is ignored.

Conceptually:

```text
Windows API
    │
    ▼
ProcessCollector
    │
    ├── valid PID → keep
    │
    └── PID 0 → discard
```

This prevents obviously invalid process records from entering the telemetry pipeline.

However, source validation does not replace backend validation.

The backend must still treat all incoming telemetry as untrusted input.

---

## 10. Network Collection

`NetworkCollector` discovers active IPv4 TCP connections.

It uses:

```text
GetExtendedTcpTable()
```

with:

```text
TCP_TABLE_OWNER_PID_ALL
```

This allows the Agent to associate network connections with process IDs.

For each connection, the Agent currently collects:

* Process ID
* Local IPv4 address
* Local port
* Remote IPv4 address
* Remote port
* TCP state

Conceptually:

```text
Windows TCP table
       │
       ▼
NetworkCollector
       │
       ▼
NetworkConnection
```

Example:

```text
PID:             1234
Local Address:   192.168.1.10
Local Port:      50000
Remote Address:  8.8.8.8
Remote Port:     443
State:           5
```

The current V1 implementation is IPv4-only.

IPv6 and richer network semantics are planned for future versions.

---

## 11. TelemetryClient

`TelemetryClient` is responsible for converting collected data into Mersad's telemetry contract.

It does not discover processes or network connections.

Instead:

```text
Collector
    ↓
TelemetryClient
    ↓
JSON telemetry envelope
```

The Agent currently has separate methods for the two implemented telemetry types:

```text
SendProcessBatch()
SendNetworkBatch()
```

Both use the same generic backend telemetry endpoint.

---

## 12. Batching

The Agent collects multiple records before sending them.

For example:

```text
ProcessCollector
      ↓
297 processes
      ↓
SendProcessBatch()
      ↓
One HTTP request
```

Similarly:

```text
NetworkCollector
      ↓
36 connections
      ↓
SendNetworkBatch()
      ↓
One HTTP request
```

Batching reduces the overhead associated with making a separate HTTP request for every event.

It also creates a natural boundary for future improvements such as:

* Maximum batch size
* Compression
* Retry
* Offline buffering
* Periodic collection
* Queue-based processing

---

## 13. JSON Construction

The current V1 Agent manually constructs JSON strings.

A process event conceptually becomes:

```json
{
  "type": "process",
  "timestamp": "2026-10-05T08:09:18Z",
  "data": {
    "processId": 1234,
    "processName": "powershell.exe"
  }
}
```

The complete request also includes the Agent identity:

```json
{
  "agentId": "GUID",
  "events": [...]
}
```

Manual JSON construction is acceptable for the small V1 prototype, but it has limitations.

For example, arbitrary strings must be escaped correctly.

A production implementation should use a proper JSON serialization library rather than manually concatenating JSON.

---

## 14. HttpClient

`HttpClient` is responsible for HTTP communication.

The Agent uses Windows WinHTTP APIs.

The separation is:

```text
TelemetryClient
    │
    │ "send this JSON"
    ▼
HttpClient
    │
    │ WinHTTP
    ▼
Mersad API
```

`TelemetryClient` therefore does not need to know the details of:

* HTTP sessions
* Connections
* Requests
* Headers
* WinHTTP handles
* Response handling

This avoids duplicating transport code across different telemetry types.

It also makes future transport changes easier.

---

## 15. Agent-to-Backend Data Flow

A process telemetry operation looks like:

```text
Windows
   │
   ▼
ProcessCollector
   │
   ▼
vector<ProcessInfo>
   │
   ▼
TelemetryClient
   │
   ▼
JSON
   │
   ▼
HttpClient
   │
   ▼
HTTP POST /api/telemetry
   │
   ▼
Mersad API
```

A network telemetry operation follows the same architecture:

```text
Windows
   │
   ▼
NetworkCollector
   │
   ▼
vector<NetworkConnection>
   │
   ▼
TelemetryClient
   │
   ▼
JSON
   │
   ▼
HttpClient
   │
   ▼
HTTP POST /api/telemetry
```

The shared transport path is one of the reasons the architecture can support additional telemetry types later.

---

## 16. Current Agent Lifecycle

The V1 Agent currently behaves like a one-shot collector.

```text
Start
  ↓
Register
  ↓
Collect processes
  ↓
Send processes
  ↓
Collect network connections
  ↓
Send network connections
  ↓
Exit
```

This is sufficient for validating the end-to-end architecture.

A production endpoint security Agent would normally remain running:

```text
Start
  ↓
Initialize
  ↓
Register
  ↓
┌──────────────────────┐
│ Collect               │
│ Queue                 │
│ Batch                 │
│ Send                  │
│ Retry                 │
│ Sleep / wait           │
└──────────┬───────────┘
           │
           └────── repeat
```

This is outside the current V1 scope.

---

## 17. Current Error Handling

The Agent currently reports failures through console output and boolean return values.

For example:

```text
Device registration request failed.
```

or:

```text
Device ID not found in API response.
```

The current implementation does not yet provide production-grade reliability mechanisms such as:

* Retry with exponential backoff
* Offline telemetry buffering
* Persistent telemetry queues
* Structured logging
* Centralized error reporting
* Agent health monitoring

These are future improvements.

---

## 18. Current Security Limitations

The V1 Agent has several intentional security limitations:

### Agent identity

`AgentId` is stored in a local file and is not cryptographically protected.

### Authentication

There is currently no strong Agent-to-API authentication mechanism.

### TLS

The V1 local development setup does not represent a production TLS/security configuration.

### JSON construction

Telemetry JSON is currently constructed manually.

### Transport reliability

There is no persistent offline queue or retry system.

These limitations are acceptable for the V1 prototype but must be addressed before production deployment.

---

## 19. Agent Design Principles

The Agent follows several important design principles.

### Single responsibility

Each component should have one primary responsibility.

```text
AgentIdentity   → identity
DeviceManager   → registration
Collector       → discovery
TelemetryClient → telemetry representation
HttpClient      → transport
```

### Validate near the source

Invalid source data should be rejected before entering the telemetry pipeline whenever possible.

### Keep transport separate from domain logic

The code that understands processes should not need to understand WinHTTP internals.

### Keep the Agent lightweight

The Agent should collect useful security telemetry while minimizing unnecessary CPU, memory, network, and disk usage.

### Design for future telemetry

The telemetry architecture should allow new collectors without requiring a completely new transport mechanism.

---

## 20. V1 Agent Scope

Implemented:

* Windows C++20 executable
* Persistent AgentId
* Device registration
* Process collection
* IPv4 TCP connection collection
* Process telemetry batching
* Network telemetry batching
* JSON telemetry construction
* WinHTTP transport

Not yet implemented:

* Continuous Agent service
* Periodic collection
* Retry/backoff
* Offline buffering
* Secure Agent enrollment
* Strong Agent authentication
* TLS production hardening
* Structured logging
* File telemetry
* Registry telemetry
* DNS telemetry
* IPv6 telemetry
* Rich process metadata
* Process creation event streaming

These features belong to future development rather than the V1 prototype.
