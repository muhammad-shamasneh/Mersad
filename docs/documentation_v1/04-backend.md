# Mersad V1 — Backend

## 1. Overview

The Mersad backend is an ASP.NET Core API responsible for receiving requests from the Mersad Agent, validating them, resolving device identity, processing telemetry, and persisting data to PostgreSQL.

The backend is intentionally simple in V1.

```text id="7h8x1m"
Mersad Agent
     │
     │ HTTP / JSON
     ▼
ASP.NET Core API
     │
     ├── Device Registration
     ├── Telemetry Ingestion
     ├── Validation
     └── Device Resolution
     │
     ▼
Entity Framework Core
     │
     ▼
PostgreSQL
```

---

## 2. Technology Stack

* ASP.NET Core
* .NET
* Entity Framework Core
* Npgsql
* PostgreSQL

The API is currently exposed locally on:

```text
http://localhost:5121
```

---

## 3. Configuration

The PostgreSQL connection is configured through:

```text
appsettings.json
```

Current connection configuration:

```json id="7n8j2q"
{
  "ConnectionStrings": {
    "MersadDb": "Host=localhost;Port=5433;Database=mersad;Username=mersad;Password=mersad_dev_password"
  }
}
```

The PostgreSQL container itself listens on port `5432`.

Docker maps it to host port `5433`:

```text id="2g5p9v"
Windows host
localhost:5433
       │
       ▼
Docker
       │
       ▼
PostgreSQL:5432
```

The API therefore connects to:

```text
localhost:5433
```

rather than directly to the container's internal port.

---

## 4. Entity Framework Core

The API uses EF Core as the persistence abstraction.

`MersadDbContext` exposes the main domain entities:

```csharp id="5x6n3b"
public DbSet<Device> Devices => Set<Device>();
public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
public DbSet<NetworkEvent> NetworkEvents => Set<NetworkEvent>();
```

The context is registered through dependency injection:

```csharp id="9v3p1k"
builder.Services.AddDbContext<MersadDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MersadDb")));
```

The controllers therefore receive the database context through constructor injection.

---

## 5. Database Relationships

The important relationships are:

```text id="2v7k9c"
Device
  │
  ├── 1 → many → ProcessEvent
  │
  └── 1 → many → NetworkEvent
```

Telemetry events reference:

```text id="0f5k2z"
Device.Id
```

The Agent's external `AgentId` is used to locate the Device, but is not the foreign key stored in telemetry tables.

---

## 6. AgentId Uniqueness

The backend enforces uniqueness for:

```text id="3m8q1x"
Devices.AgentId
```

through an EF Core index:

```csharp id="0w5z8f"
modelBuilder.Entity<Device>()
    .HasIndex(d => d.AgentId)
    .IsUnique();
```

This is important because `AgentId` represents one Agent installation.

The database therefore prevents two Device records from being created with the same AgentId.

---

## 7. Device Registration API

The registration endpoint is:

```text id="8f4h2p"
POST /api/devices
```

The Agent sends:

```json id="6v1x9k"
{
  "agentId": "...",
  "hostname": "...",
  "os": "Windows",
  "osVersion": "11"
}
```

The backend searches for the AgentId.

### Existing device

If found:

```text id="4p9k7c"
AgentId
   ↓
Existing Device
   ↓
Update metadata
   ↓
Save
   ↓
Return Device
```

The backend updates:

* Hostname
* OS
* OSVersion
* LastSeen

### New device

If no Device exists:

```text id="7g2m1v"
AgentId
   ↓
Create Device
   ↓
Generate Device.Id
   ↓
Save
   ↓
Return Device
```

---

## 8. Telemetry API

Telemetry is received through:

```text id="1x7n4b"
POST /api/telemetry
```

The endpoint accepts:

```text id="4k9p2z"
TelemetryRequest
```

containing:

```text id="5q1v8m"
AgentId
Events[]
```

The controller is responsible for ingestion rather than exposing individual endpoints for each telemetry type.

---

## 9. Telemetry Processing

The backend processing sequence is:

```text id="3b8n6x"
Receive request
     │
     ▼
Validate AgentId
     │
     ▼
Validate event collection
     │
     ▼
Resolve AgentId → Device
     │
     ▼
Validate each event
     │
     ▼
Determine event type
     │
     ├── process
     │      ↓
     │  ProcessTelemetryData
     │      ↓
     │  ProcessEvent
     │
     └── network
            ↓
        NetworkTelemetryData
            ↓
        NetworkEvent
     │
     ▼
SaveChangesAsync()
     │
     ▼
Return response
```

---

## 10. AgentId → Device.Id Resolution

This is an important backend responsibility.

The API receives:

```text id="n4f7p1"
AgentId
```

It searches:

```text id="x3q6k8"
Devices.AgentId
```

and obtains:

```text id="p7z2m5"
Device.Id
```

The domain event is then created using the internal ID:

```csharp id="r8y3v2"
var processEvent = new ProcessEvent
{
    Id = Guid.NewGuid(),
    DeviceId = device.Id,
    ...
};
```

This means the controller performs the identity translation at the boundary.

After that point, the rest of the database logic works with the internal `Device.Id`.

---

## 11. Validation Strategy

The backend performs validation before persistence.

Validation exists at multiple levels.

### Request

```text id="5c8h1q"
AgentId must exist
Events must not be empty
```

### Event

```text id="8m2v6j"
Type must exist
Timestamp must be within allowed range
```

### Typed payload

Process:

```text id="2g7x9p"
ProcessId > 0
ProcessName != empty
```

Network:

```text id="0v5c4n"
ProcessId > 0
Addresses != empty
Ports between 0 and 65535
State != empty
```

The backend therefore does not assume that a valid JSON structure necessarily means valid telemetry.

---

## 12. Database Persistence

EF Core tracks the newly created domain events.

The API adds events to the context:

```text id="4q8m1z"
_db.ProcessEvents.Add(...)
```

or:

```text id="9p3v6x"
_db.NetworkEvents.Add(...)
```

After processing the batch:

```csharp id="1k6r8v"
await _db.SaveChangesAsync();
```

is called.

This allows the batch to be persisted through a single save operation instead of issuing a database operation for every event.

---

## 13. Device LastSeen

Whenever telemetry is successfully processed, the backend updates:

```text id="6x2m8q"
Device.LastSeen
```

This gives the backend a basic indication of when the Agent last communicated successfully.

Conceptually:

```text id="3n7p1v"
Telemetry received
      ↓
Device.LastSeen = now
```

This will later be useful for device health and online/offline status.

---

## 14. Controllers

The current API contains controllers for the main V1 operations.

### DevicesController

Responsible for:

* Listing devices
* Getting a device
* Registering/updating a device

### TelemetryController

Responsible for:

* Receiving telemetry
* Validating telemetry
* Resolving device identity
* Converting transport models into domain events
* Persisting telemetry

The controllers currently contain some business logic because V1 is intentionally small.

As the system grows, this logic should move toward application/service layers.

---

## 15. Current Architectural Simplification

V1 does not yet have a full layered architecture such as:

```text id="4z7m2p"
Controller
    ↓
Application Service
    ↓
Domain
    ↓
Repository
    ↓
EF Core
```

Instead, the current structure is closer to:

```text id="7c1m9v"
Controller
    ↓
EF Core DbContext
    ↓
PostgreSQL
```

This is intentional.

Introducing repositories, service abstractions, CQRS, MediatR, or similar patterns before the product needs them would add complexity without solving a current problem.

The architecture should become more layered when the business logic actually becomes complex enough to justify it.

---

## 16. Database Migrations

EF Core migrations are used to evolve the PostgreSQL schema.

For example, when network telemetry was introduced, a migration was created:

```text id="8k3v6q"
20261004130133_AddNetworkEvents
```

The migration represents the schema change required to add network-event persistence.

The intended workflow is:

```text id="2m7x9c"
Change domain model
      ↓
Create migration
      ↓
Review migration
      ↓
Apply migration
      ↓
Database schema updated
```

This keeps database schema changes version-controlled alongside the application.

---

## 17. Current API Responsibilities

The backend currently owns:

* Device registration
* Device identity resolution
* Request validation
* Telemetry validation
* Telemetry type interpretation
* Domain event creation
* Persistence
* Device `LastSeen`

It does **not yet** own:

* Authentication
* Authorization
* Detection rules
* Alert generation
* Investigation workflows
* AI analysis
* Background telemetry processing
* Queue management

Those capabilities belong to later stages of the V1/V2 architecture.

---

## 18. API Security Status

The V1 API currently does not authenticate Agents.

The following are therefore not implemented yet:

```text
Agent authentication
API keys
Certificates
JWT
Agent enrollment
Authorization
```

This means the current API should be considered a development/prototype API rather than a production security boundary.

Authentication is a future hardening requirement.

---

## 19. Current Backend Limitations

Important limitations include:

* No Agent authentication
* No authorization
* No rate limiting
* No request-size limits specific to telemetry
* No queue between API and persistence
* No retry mechanism
* Limited structured logging
* Controllers contain some business logic
* No dedicated application/service layer
* No production database credentials/secrets management
* No telemetry retention strategy
* No database partitioning
* No alert/detection persistence yet

These are intentional V1 simplifications.

---

## 20. Backend Design Principles

### Keep external identity at the boundary

Resolve:

```text id="7j2m5q"
AgentId → Device.Id
```

once and use the internal identity afterward.

### Validate before persistence

The API should never assume that data received from the Agent is trustworthy.

### Keep V1 simple

Do not introduce architectural abstractions without a real problem to solve.

### Keep schema changes version-controlled

Database migrations should be committed alongside the application changes that require them.

### Separate transport models from persistence models

The HTTP contract should not be forced to match the database schema.

---

## 21. Backend V1 Summary

The V1 backend provides the minimal infrastructure required to turn Agent telemetry into persistent security data:

```text id="9c6m1x"
Agent
  ↓
HTTP API
  ↓
Validation
  ↓
AgentId resolution
  ↓
Typed telemetry
  ↓
Domain event
  ↓
EF Core
  ↓
PostgreSQL
```

The backend is intentionally not over-engineered.

Its primary purpose in V1 is to establish a reliable ingestion and persistence path that later detection, alerting, investigation, and AI features can build upon.
