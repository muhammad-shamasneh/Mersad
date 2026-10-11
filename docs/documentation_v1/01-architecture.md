# Mersad V1 — Architecture

## 1. Overview

Mersad V1 is a Windows-first security telemetry platform.

Its purpose is to collect endpoint telemetry from a Windows machine, send it to a backend, persist it, and provide the foundation for detection, alerting, investigation, and AI-assisted analysis.

The V1 architecture follows this flow:

```text
Windows Endpoint
      │
      ▼
Mersad Agent (C++)
      │
      │ HTTP / JSON
      ▼
Mersad API (ASP.NET Core)
      │
      ├── Validation
      ├── Device resolution
      ├── Telemetry processing
      │
      ▼
PostgreSQL
      │
      ▼
Detection / Alerting
      │
      ▼
Dashboard
      │
      ▼
Investigation / AI Explanation
```

The architecture is intentionally small for V1. It provides a working vertical slice that can later be expanded into a more complete endpoint security platform.

---

## 2. Main Components

### 2.1 Mersad Agent

Technology:

* C++20
* Windows APIs
* WinHTTP

Location:

```text
agent/Mersad.Agent/
```

The Agent runs on the Windows endpoint and is responsible for:

* Maintaining its persistent `AgentId`
* Registering the endpoint with the backend
* Collecting process telemetry
* Collecting IPv4 TCP connection telemetry
* Building telemetry batches
* Sending telemetry to the backend

The Agent should focus on **collection and transport**, not backend business logic.

---

### 2.2 Mersad API

Technology:

* ASP.NET Core
* .NET
* Entity Framework Core
* Npgsql

Location:

```text
backend/Mersad.Api/
```

The API is the backend entry point for the Agent.

Its responsibilities include:

* Device registration
* Receiving telemetry
* Validating incoming data
* Resolving `AgentId` to the internal `Device.Id`
* Converting transport telemetry into domain models
* Persisting telemetry
* Updating device `LastSeen`

The backend does not blindly trust data received from the Agent.

---

### 2.3 PostgreSQL

PostgreSQL stores the persistent state of Mersad.

Current major entities include:

```text
Devices
ProcessEvents
NetworkEvents
```

The database provides the persistent relationship between a device and its telemetry.

Conceptually:

```text
Device
 ├── ProcessEvents
 └── NetworkEvents
```

---

### 2.4 Dashboard

Technology:

* React
* TypeScript

Location:

```text
dashboard/mersad-console/
```

The dashboard is intended to provide:

* Device visibility
* Alert feed
* Alert investigation
* Telemetry context
* AI-generated alert explanations

The dashboard consumes backend APIs rather than communicating directly with the Agent or PostgreSQL.

---

## 3. Agent Identity and Device Identity

Mersad intentionally separates the Agent's identity from the backend's database identity.

### AgentId

`AgentId` identifies the Mersad Agent installation.

The Agent stores it persistently in:

```text
Mersad.AgentId
```

On startup:

```text
Load AgentId
     │
     ├── exists → use it
     │
     └── missing → generate and save it
```

The Agent sends this identifier to the backend.

### Device.Id

`Device.Id` is the backend's internal identity for the device record.

The relationship is:

```text
Agent installation
       │
       ▼
    AgentId
       │
       ▼
Devices.AgentId
       │
       ▼
    Device.Id
       │
       ├── ProcessEvents
       └── NetworkEvents
```

`AgentId` is therefore part of the Agent/backend contract, while `Device.Id` is the database's internal identity.

Telemetry events store `Device.Id`, not `AgentId`.

This keeps the database model decoupled from the external Agent identity mechanism.

---

## 4. Device Registration Flow

When the Agent starts:

```text
1. Load or create AgentId
2. Collect basic device information
3. POST /api/devices
4. Backend searches for AgentId
5. Existing device?
       │
       ├── Yes → update existing Device
       │
       └── No → create new Device
6. Backend returns Device.Id
```

Current device information includes:

* AgentId
* Hostname
* Operating system
* OS version
* LastSeen

`AgentId` has a unique database constraint.

Therefore, restarting the same Agent does not create another Device record.

A different Agent installation receives a different `AgentId`.

---

## 5. Telemetry Architecture

Mersad uses a generic telemetry envelope instead of creating a separate HTTP endpoint for every telemetry type.

Example:

```json
{
  "agentId": "GUID",
  "events": [
    {
      "type": "process",
      "timestamp": "2026-10-04T12:10:00Z",
      "data": {
        "processId": 1234,
        "processName": "powershell.exe"
      }
    },
    {
      "type": "network",
      "timestamp": "2026-10-04T12:10:01Z",
      "data": {
        "processId": 5678,
        "localAddress": "192.168.1.10",
        "localPort": 50000,
        "remoteAddress": "8.8.8.8",
        "remotePort": 443,
        "state": "5"
      }
    }
  ]
}
```

The envelope contains:

* `agentId` — identifies the sending Agent
* `events` — collection of telemetry events

Each event contains:

* `type` — identifies the telemetry category
* `timestamp` — event timestamp
* `data` — type-specific payload

The `type` allows the backend to determine how the `data` should be interpreted.

For example:

```text
type = process
        ↓
ProcessTelemetryData

type = network
        ↓
NetworkTelemetryData
```

This allows future telemetry types to use the same ingestion endpoint.

---

## 6. Why JsonElement Is Used

The backend receives telemetry containing different data structures.

For example:

```text
process → ProcessTelemetryData
network → NetworkTelemetryData
```

`TelemetryEvent.Data` therefore uses `JsonElement`.

The backend first receives the JSON structure generically:

```text
JsonElement
```

Then, after checking `Type`, it deserializes the data into the appropriate strongly typed model.

```text
JSON
 │
 ▼
JsonElement
 │
 ├── type = process
 │       ↓
 │   ProcessTelemetryData
 │
 └── type = network
         ↓
     NetworkTelemetryData
```

This avoids forcing all telemetry types into one rigid data structure.

---

## 7. Why Telemetry Is Batched

The Agent does not send one HTTP request for every process or network event.

Instead:

```text
Collect many events
       │
       ▼
Create one batch
       │
       ▼
One HTTP request
```

Batching reduces:

* HTTP request overhead
* Network overhead
* Serialization overhead
* API request processing overhead
* Database transaction overhead

It also provides a more scalable foundation for future telemetry volume.

V1 uses simple in-memory batching.

---

## 8. Trust Boundaries and Validation

The Agent validates telemetry close to the source.

For example, the process collector ignores PID `0` because it is not considered a valid process identity for Mersad process telemetry.

However, the backend validates the data again.

The reason is that the Agent is an external client from the backend's perspective.

The Agent could be:

* Modified
* Compromised
* Replaced by a fake client
* Sending malformed data

Therefore:

```text
Agent
  │
  │ validate
  ▼
HTTP boundary
  │
  ▼
API
  │
  │ validate again
  ▼
Database
```

Validation at the source improves data quality.

Validation at the backend protects the backend and database from untrusted input.

---

## 9. Telemetry Processing

When the backend receives telemetry:

```text
POST /api/telemetry
        │
        ▼
Validate AgentId
        │
        ▼
Find Device using AgentId
        │
        ▼
Validate event structure
        │
        ▼
Inspect event type
        │
        ├── process → ProcessEvent
        │
        └── network → NetworkEvent
        │
        ▼
Save to PostgreSQL
        │
        ▼
Update Device.LastSeen
```

The backend stores telemetry using the internal `Device.Id`.

This means the telemetry database does not need to know how the Agent generated or stores its identity.

---

## 10. Current Network Telemetry

V1 collects IPv4 TCP connection information using Windows `GetExtendedTcpTable`.

The Agent collects:

* Process ID
* Local IPv4 address
* Local port
* Remote IPv4 address
* Remote port
* TCP state

The network collector currently focuses on IPv4.

IPv6 and additional network coverage are planned for future versions.

---

## 11. Component Responsibilities

The major design responsibility boundaries are:

| Component             | Responsibility                     |
| --------------------- | ---------------------------------- |
| `ProcessCollector`    | Discover Windows processes         |
| `NetworkCollector`    | Discover network connections       |
| `AgentIdentity`       | Create/load persistent AgentId     |
| `DeviceManager`       | Register the device                |
| `TelemetryClient`     | Build and send telemetry payloads  |
| `HttpClient`          | Perform HTTP communication         |
| `DevicesController`   | Device registration                |
| `TelemetryController` | Telemetry ingestion and validation |
| EF Core               | Database access and persistence    |
| PostgreSQL            | Persistent storage                 |
| Dashboard             | Visualization and investigation    |

The goal is to avoid making one component responsible for unrelated concerns.

For example, `TelemetryClient` should not contain WinHTTP implementation details, and `HttpClient` should not know what a process event means.

---

## 12. V1 End-to-End Flow

A complete V1 flow is:

```text
Windows machine
      │
      ▼
Mersad Agent starts
      │
      ▼
Load/Create AgentId
      │
      ▼
Register Device
      │
      ▼
Backend resolves AgentId → Device.Id
      │
      ▼
Collect process telemetry
      │
      ▼
Collect network telemetry
      │
      ▼
Create telemetry batches
      │
      ▼
HTTP POST
      │
      ▼
TelemetryController
      │
      ├── Validate request
      ├── Resolve device
      ├── Validate event data
      └── Convert to domain models
      │
      ▼
PostgreSQL
      │
      ▼
Detection Engine
      │
      ▼
Alerts
      │
      ▼
Dashboard
      │
      ▼
Investigation
      │
      ▼
AI Alert Explanation
```

This is the intended V1 vertical slice.

---

## 13. Important V1 Design Principles

### Validate close to the source

Invalid data should be rejected as early as practical.

### Never blindly trust clients

Backend validation is required even when the client is our own Agent.

### Separate external and internal identity

`AgentId` identifies the Agent installation.

`Device.Id` identifies the backend Device record.

### Separate responsibilities

Collection, transport, API processing, and persistence should not be unnecessarily coupled.

### Prefer extensible contracts

The generic telemetry envelope allows additional event types without creating a completely separate ingestion architecture for every type.

### Batch where appropriate

Telemetry should be transported in batches rather than generating one HTTP request per event.

---

## 14. V1 Security Limitation

The current Agent identity mechanism is not strong authentication.

The AgentId is stored in a local file:

```text
Mersad.AgentId
```

If an attacker copies the file to another machine, that machine can present the same AgentId.

The backend currently has no additional mechanism to prove that the sender is the original machine.

Therefore:

```text
AgentId = identity claim
```

does **not** mean:

```text
AgentId = authenticated machine
```

This is an intentional V1 limitation.

Future versions should introduce stronger agent enrollment and authentication mechanisms.

---

## 15. V1 Scope

Implemented:

* Windows C++ Agent
* Persistent AgentId
* Device registration
* Process telemetry
* IPv4 TCP network telemetry
* Generic telemetry envelope
* Batched telemetry ingestion
* ASP.NET Core API
* PostgreSQL persistence

Implemented in the verified V1 vertical slice:

* Detection engine with registered rule-based detections
* Alert model and PostgreSQL persistence
* Alerts API
* React dashboard alert feed
* End-to-end telemetry-to-alert data path

Still required or planned for future development:

* Investigation page and investigation workflows
* AI-assisted alert explanations
* Production-grade authentication and authorization
* Comprehensive end-to-end, security, and load testing

The telemetry-to-alert path is implemented, but this does not mean every planned V1 capability is complete or that the product is production-ready.