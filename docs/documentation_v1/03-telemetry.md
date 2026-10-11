# Mersad V1 — Telemetry

## 1. Overview

Telemetry is the primary data flow between the Mersad Agent and the backend.

The Agent collects endpoint information, converts it into the Mersad telemetry contract, batches it, and sends it to the API.

The backend receives the telemetry, validates it, determines the telemetry type, converts it into the appropriate domain model, and persists it.

The overall flow is:

```text id="5n1x1v"
Windows
   │
   ▼
Collector
   │
   ▼
Internal C++ structure
   │
   ▼
TelemetryClient
   │
   ▼
JSON telemetry envelope
   │
   ▼
HTTP POST
   │
   ▼
TelemetryController
   │
   ▼
Validation
   │
   ▼
Typed telemetry data
   │
   ▼
Domain event
   │
   ▼
PostgreSQL
```

---

# 2. Telemetry Contract

Mersad uses a generic telemetry envelope.

The current contract is:

```json id="qntj62"
{
  "agentId": "GUID",
  "events": [
    {
      "type": "process",
      "timestamp": "2026-10-05T08:09:18Z",
      "data": {
        "processId": 1234,
        "processName": "powershell.exe"
      }
    }
  ]
}
```

The top-level request contains:

| Field     | Purpose                                                 |
| --------- | ------------------------------------------------------- |
| `agentId` | Identifies the Agent installation sending the telemetry |
| `events`  | Collection of telemetry events                          |

Each event contains:

| Field       | Purpose                             |
| ----------- | ----------------------------------- |
| `type`      | Identifies the telemetry category   |
| `timestamp` | Timestamp associated with the event |
| `data`      | Type-specific event data            |

---

# 3. Why Use a Generic Envelope?

Mersad could have created separate endpoints such as:

```text id="0m7f6h"
POST /api/process-events
POST /api/network-events
POST /api/file-events
POST /api/dns-events
...
```

Instead, V1 uses:

```text id="7s2e8y"
POST /api/telemetry
```

with a type field:

```text id="l9p4fz"
"type": "process"
```

or:

```text id="d4z4w9"
"type": "network"
```

This gives the telemetry system a common ingestion mechanism.

Adding a new telemetry type can therefore follow the same general pipeline:

```text id="3j0d1x"
Collector
   ↓
TelemetryClient
   ↓
Telemetry envelope
   ↓
POST /api/telemetry
   ↓
Backend identifies type
   ↓
Typed data
   ↓
Domain model
```

---

# 4. Event Type

The `type` field identifies what the `data` field represents.

For V1:

```text id="8s6y9a"
process → ProcessTelemetryData
network → NetworkTelemetryData
```

For example:

```json id="5x0a5r"
{
  "type": "process",
  "timestamp": "2026-10-05T08:09:18Z",
  "data": {
    "processId": 1234,
    "processName": "powershell.exe"
  }
}
```

The backend sees:

```text id="p1h5c0"
type = process
```

and therefore interprets the `data` as:

```text id="f6i3n0"
ProcessTelemetryData
```

For:

```json id="7qj2km"
{
  "type": "network",
  "timestamp": "2026-10-05T08:09:18Z",
  "data": {
    "processId": 5678,
    "localAddress": "192.168.1.10",
    "localPort": 50000,
    "remoteAddress": "8.8.8.8",
    "remotePort": 443,
    "state": "5"
  }
}
```

the backend interprets the data as:

```text id="1fs9gt"
NetworkTelemetryData
```

---

# 5. Transport Model vs Domain Model

A key architectural distinction is the difference between the **transport representation** and the **database/domain representation**.

The transport model is designed for communication between the Agent and API.

The domain model represents persisted data.

For example:

```text id="m3p5t9"
TelemetryEvent
      │
      │ transport representation
      ▼
ProcessTelemetryData
      │
      │ backend interpretation
      ▼
ProcessEvent
      │
      │ persistence
      ▼
PostgreSQL
```

The transport object does not need to be identical to the database object.

This separation allows the API contract and database schema to evolve independently.

---

# 6. TelemetryRequest

The backend represents the incoming request as:

```csharp id="c9c3jr"
public class TelemetryRequest
{
    public Guid AgentId { get; set; }

    public List<TelemetryEvent> Events { get; set; }
        = new();
}
```

The request therefore represents:

```text id="u3g5b0"
One Agent
    │
    └── Many telemetry events
```

The `AgentId` belongs to the request rather than being repeated inside every event.

This avoids unnecessary duplication.

---

# 7. TelemetryEvent

The generic event is represented as:

```csharp id="k6x6x0"
public class TelemetryEvent
{
    public string Type { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public JsonElement Data { get; set; }
}
```

The important design decision is:

```text id="r8g6f1"
Data → JsonElement
```

rather than immediately forcing the data into one concrete C# type.

---

# 8. Why JsonElement?

Different telemetry types have different schemas.

For example:

```text id="n2k0km"
Process
{
    ProcessId
    ProcessName
}
```

while:

```text id="n8n0d7"
Network
{
    ProcessId
    LocalAddress
    LocalPort
    RemoteAddress
    RemotePort
    State
}
```

There is no single useful strongly typed class that naturally represents both payloads.

`JsonElement` allows the backend to preserve the JSON structure until the event type is known.

The processing becomes:

```text id="3s1e2g"
JSON
 │
 ▼
TelemetryEvent
 │
 ├── Type
 ├── Timestamp
 └── Data (JsonElement)
       │
       ▼
Determine Type
       │
       ├── process → deserialize ProcessTelemetryData
       │
       └── network → deserialize NetworkTelemetryData
```

---

# 9. Typed Telemetry Data

After identifying the event type, the backend converts the generic JSON payload into a strongly typed object.

## ProcessTelemetryData

```csharp id="9n4q5v"
public class ProcessTelemetryData
{
    public int ProcessId { get; set; }

    public string ProcessName { get; set; } = string.Empty;
}
```

## NetworkTelemetryData

```csharp id="9x8r1s"
public class NetworkTelemetryData
{
    public int ProcessId { get; set; }

    public string LocalAddress { get; set; } = string.Empty;

    public int LocalPort { get; set; }

    public string RemoteAddress { get; set; } = string.Empty;

    public int RemotePort { get; set; }

    public string State { get; set; } = string.Empty;
}
```

Typed models allow the backend to perform meaningful validation and create domain objects.

---

# 10. Important Serialization Issue

An earlier implementation used:

```text id="p2g5h1"
Dictionary<string, object>
```

for the telemetry data.

This caused an important problem.

The JSON payload was converted into a dictionary and then later serialized/deserialized again.

The resulting object did not map correctly into the typed process model because of property-name handling.

For example, the backend could end up with:

```text id="d4v0q6"
ProcessId = 0
ProcessName = ""
```

even though the JSON contained valid values.

The V1 implementation was changed to use `JsonElement` and explicit case-insensitive deserialization:

```csharp id="5y7w1q"
new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}
```

This keeps the incoming JSON structure intact until the backend knows which concrete type it should become.

---

# 11. AgentId Resolution

The Agent sends:

```text id="n9g1pl"
AgentId
```

The Agent does **not** send the database `Device.Id`.

The backend performs:

```text id="s3z6kj"
AgentId
   │
   ▼
Devices.AgentId
   │
   ▼
Device.Id
```

The resulting `Device.Id` is then stored in the telemetry event.

For example:

```text id="k7m3y2"
AgentId = ABC
       ↓
Device.Id = 123
       ↓
ProcessEvent.DeviceId = 123
```

This keeps external Agent identity separate from internal database identity.

---

# 12. Why Telemetry Stores Device.Id

Telemetry events store:

```text id="1w7c4r"
DeviceId
```

rather than:

```text id="b7f9s2"
AgentId
```

This gives the database a stable internal relationship:

```text id="z0m4gq"
Device
  │
  ├── ProcessEvent
  ├── ProcessEvent
  ├── NetworkEvent
  └── NetworkEvent
```

The Agent's identity mechanism can change without forcing every telemetry table to change.

The backend resolves the external identity once at the API boundary and uses its internal identity afterward.

---

# 13. Telemetry Validation

The backend validates incoming telemetry before persistence.

Validation happens at several levels.

## Request validation

The backend checks:

```text id="z4q2c5"
AgentId != Guid.Empty
Events != null
Events.Count > 0
```

## Timestamp validation

The current V1 rules are:

```text id="6j7w0k"
Timestamp <= current time + 5 minutes
Timestamp >= current time - 24 hours
```

This prevents obviously invalid timestamps from entering the database.

## Event validation

The backend checks that:

```text id="x2g6j5"
Type is not empty
```

and that the payload is valid for the specified type.

---

# 14. Process Telemetry Validation

For process telemetry, the backend currently requires:

```text id="z9q4m2"
ProcessId > 0
ProcessName is not empty
```

If the data is invalid, the API rejects the request.

Example:

```text id="6p1r0q"
ProcessId = 0
        ↓
Invalid process telemetry
        ↓
HTTP 400
```

The Agent also filters PID `0` before sending.

This gives us validation at both ends:

```text id="9s0y5k"
Agent validation
        +
Backend validation
```

---

# 15. Network Telemetry Validation

Network telemetry is validated for:

```text id="e4z6b8"
ProcessId > 0

LocalAddress is not empty

LocalPort >= 0
LocalPort <= 65535

RemoteAddress is not empty

RemotePort >= 0
RemotePort <= 65535

State is not empty
```

The backend therefore does not blindly persist arbitrary network data.

---

# 16. Unsupported Event Types

The backend explicitly handles known event types.

For example:

```text id="9s5e3c"
process → supported
network → supported
```

If an unsupported type arrives:

```text id="8u4j2w"
type = "something-unknown"
```

the backend returns a bad-request response rather than silently accepting it.

This prevents unknown data from entering the database without a defined interpretation.

---

# 17. Batching

The Agent sends multiple events in a single request.

For example:

```text id="5w1t6d"
POST /api/telemetry

AgentId = ABC

Events:
  1. process
  2. process
  3. process
  ...
  297. process
```

This is preferable to:

```text id="5a8m1n"
POST process
POST process
POST process
...
POST process
```

for every individual event.

Batching reduces repeated overhead at multiple layers:

```text id="f7g4h2"
Agent
  ↓
Serialization
  ↓
Network
  ↓
HTTP
  ↓
API
  ↓
Database
```

---

# 18. Batch Persistence

The backend adds all valid events to the EF Core context during processing.

It then calls:

```csharp id="3x1j5z"
await _db.SaveChangesAsync();
```

after processing the batch.

Conceptually:

```text id="m9v4q2"
Receive batch
     │
     ▼
Validate events
     │
     ▼
Add domain events to EF
     │
     ▼
SaveChangesAsync()
     │
     ▼
PostgreSQL
```

This avoids issuing a separate database save for every event.

---

# 19. Batch Failure Behavior

The current implementation performs one database save after processing the events.

Therefore, if validation fails before the save operation, the request is rejected and the accumulated changes are not persisted.

This gives V1 a simple all-or-nothing behavior at the database save boundary.

For example:

```text id="7k2q0p"
100 events
    │
    ├── first 99 valid
    │
    └── event 100 invalid
             ↓
        request rejected
```

The current implementation does not partially persist the first 99 events.

Future versions may need more sophisticated handling for very large batches or partial failures.

---

# 20. Timestamp Model

The telemetry contract currently contains an event timestamp.

The Agent currently generates one timestamp for a telemetry batch rather than maintaining an independent precise timestamp for every collected event.

For example:

```text id="8h5p2s"
Batch timestamp = T

Process 1 → T
Process 2 → T
Process 3 → T
...
```

This is acceptable for the V1 prototype.

A production telemetry system should generally capture timestamps closer to the actual collection time of each event.

---

# 21. Current Telemetry Types

V1 currently supports:

### Process

```text id="5c4v0n"
ProcessId
ProcessName
Timestamp
DeviceId
```

### Network

```text id="5c4v0n"
ProcessId
LocalAddress
LocalPort
RemoteAddress
RemotePort
State
Timestamp
DeviceId
```

The system is intentionally designed so that future telemetry types can follow the same ingestion model.

Potential future types include:

```text id="n0r3q7"
File
Registry
DNS
Service
Login
Driver
Module
PowerShell
Process creation
```

These are future scope, not V1 requirements.

---

# 22. Complete Telemetry Lifecycle

A process event travels through the system as follows:

```text id="9p8h2j"
Windows process
      │
      ▼
ProcessCollector
      │
      ▼
ProcessInfo
      │
      ▼
TelemetryClient
      │
      ▼
JSON
      │
      ▼
HTTP POST
      │
      ▼
TelemetryController
      │
      ├── Validate AgentId
      │
      ├── Find Device
      │
      ├── Validate timestamp
      │
      ├── Validate event type
      │
      ├── Deserialize data
      │
      ├── Validate ProcessTelemetryData
      │
      └── Create ProcessEvent
              │
              ▼
          Device.Id
              │
              ▼
         PostgreSQL
```

A network event follows the same pipeline but becomes a `NetworkEvent`.

---

# 23. Telemetry Design Principles

## Generic transport contract

The HTTP layer should not need a completely different protocol for every event type.

## Strong typing after classification

The backend keeps telemetry generic until the event type is known, then converts it into a strongly typed model.

## Validate at trust boundaries

The Agent validates source data.

The API validates incoming external data again.

## Separate transport and persistence models

The JSON contract is not required to match the database schema.

## Use internal database identity

External `AgentId` is resolved to internal `Device.Id` before telemetry is persisted.

## Batch telemetry

Multiple events are transported and persisted together to reduce overhead.

---

# 24. Current Limitations

The V1 telemetry implementation intentionally has several limitations:

* Manual JSON construction in the C++ Agent
* No compression
* No authentication
* No retry/backoff
* No offline buffering
* No maximum batch-size policy
* One batch timestamp rather than precise per-event timestamps
* IPv4-focused network telemetry
* Limited process metadata
* No event deduplication
* No event sequence numbers
* No telemetry encryption design beyond the eventual transport layer
* No schema version field

These are candidates for future hardening.

---

# 25. Future Telemetry Direction

The V2 telemetry system should evolve toward a more production-oriented pipeline:

```text id="6g3x8c"
Collector
    ↓
Local event queue
    ↓
Batcher
    ↓
Serializer
    ↓
Authenticated transport
    ↓
API
    ↓
Validation
    ↓
Ingestion pipeline
    ↓
Detection
    ↓
Storage
```

Potential improvements include:

* Schema versioning
* Proper JSON serialization
* Per-event timestamps
* Maximum batch sizes
* Compression
* Retry with exponential backoff
* Offline buffering
* Authentication
* TLS hardening
* Event IDs
* Sequence numbers
* Better network semantics
* Telemetry retention policies

These improvements should be introduced deliberately rather than making V1 unnecessarily complex.
