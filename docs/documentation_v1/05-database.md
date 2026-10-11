# Mersad V1 — Database

## 1. Overview

Mersad V1 uses PostgreSQL as its persistent data store.

The database stores:

* Registered devices
* Process telemetry
* Network telemetry

The current database is intentionally small and designed around the V1 telemetry pipeline.

```text
id="d7k3m1"
Devices
   │
   ├── ProcessEvents
   │
   └── NetworkEvents
```

---

## 2. PostgreSQL Configuration

The V1 PostgreSQL instance runs through Docker.

```text
Docker container:
mersad-postgres

Database:
mersad

Username:
mersad

Host port:
5433

Container port:
5432
```

The Docker configuration uses a persistent volume:

```text
mersad-postgres-data
```

This allows database data to survive container restarts.

---

## 3. Current Schema

The V1 schema contains three main tables:

```text
Devices
ProcessEvents
NetworkEvents
```

Conceptually:

```text
                    ┌──────────────────┐
                    │     Devices      │
                    │──────────────────│
                    │ Id (PK)           │
                    │ AgentId (Unique)  │
                    │ Hostname          │
                    │ OS                │
                    │ OSVersion         │
                    │ LastSeen          │
                    └────────┬─────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
                ▼                         ▼
       ┌─────────────────┐      ┌─────────────────┐
       │ ProcessEvents   │      │ NetworkEvents   │
       │─────────────────│      │─────────────────│
       │ Id (PK)         │      │ Id (PK)         │
       │ DeviceId (FK)   │      │ DeviceId (FK)   │
       │ ProcessId       │      │ ProcessId       │
       │ ProcessName     │      │ LocalAddress   │
       │ Timestamp       │      │ LocalPort      │
       └─────────────────┘      │ RemoteAddress  │
                                │ RemotePort     │
                                │ State          │
                                │ Timestamp      │
                                └─────────────────┘
```

---

# 4. Devices

The `Devices` table represents a registered Mersad Agent/device record.

Current model:

```csharp id="8h2m6q"
public class Device
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    public string Hostname { get; set; } = string.Empty;

    public string OS { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public DateTime LastSeen { get; set; }
}
```

### Id

`Id` is the backend's internal identifier for the Device record.

It is the primary key.

### AgentId

`AgentId` identifies the Mersad Agent installation.

It is stored in the database because the backend needs to resolve incoming Agent requests:

```text
AgentId
   ↓
Devices.AgentId
   ↓
Device.Id
```

`AgentId` has a unique constraint.

### Hostname

The hostname provides descriptive information about the machine.

It is **not** used as the primary identity mechanism because hostnames are not guaranteed to be unique.

### OS / OSVersion

These fields describe the endpoint operating system.

They are currently simple strings because V1 does not require a sophisticated operating-system model.

### LastSeen

`LastSeen` represents the last time the backend successfully communicated with the Agent.

It provides the foundation for future device health/online status.

---

# 5. AgentId vs Device.Id

This distinction is fundamental to the V1 database design.

```text
AgentId
    ↓
External / Agent-facing identity

Device.Id
    ↓
Internal / Database identity
```

For example:

```text
AgentId:
0ca418f5-51ce-4eb9-880f-3e2ec91494fd

Device.Id:
401ad2f3-98ea-4afe-8906-4218656a071a
```

The values are different and have different purposes.

The Agent sends:

```text
AgentId
```

The backend resolves it to:

```text
Device.Id
```

Telemetry stores:

```text
Device.Id
```

This keeps the database's internal relationships independent from the Agent identity mechanism.

---

# 6. AgentId Uniqueness

The database enforces:

```text
Devices.AgentId = UNIQUE
```

This prevents two Device records from having the same AgentId.

The relationship is therefore:

```text
One AgentId
    ↓
One Device record
```

This is important for the current V1 identity model.

If the same Agent restarts, it presents the same AgentId and the existing Device is updated.

A new Agent installation should receive a new AgentId.

---

# 7. ProcessEvents

`ProcessEvents` stores process telemetry received from the Agent.

Current model:

```csharp id="5k7p1x"
public class ProcessEvent
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public int ProcessId { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
```

### Id

Unique identifier for the telemetry record.

### DeviceId

Foreign key to:

```text
Devices.Id
```

This associates the process event with the device that generated it.

### ProcessId

Windows process identifier observed by the Agent.

Important:

> A PID is not globally unique and should not be treated as a permanent process identity.

A Windows process can terminate and another process can later reuse the same PID.

V1 therefore stores the PID as telemetry data rather than using it as a database identity.

### ProcessName

The executable/process name observed by the Agent.

Examples:

```text
powershell.exe
chrome.exe
svchost.exe
```

### Timestamp

The timestamp associated with the telemetry event.

---

# 8. NetworkEvents

`NetworkEvents` stores IPv4 TCP connection telemetry.

Current model:

```text
Id
DeviceId
ProcessId
LocalAddress
LocalPort
RemoteAddress
RemotePort
State
Timestamp
```

The `DeviceId` is a foreign key to:

```text
Devices.Id
```

The `ProcessId` associates the network connection with the Windows process that owns it.

This allows the system to correlate:

```text
Process
    +
Network connection
```

For example:

```text
powershell.exe
    │
    └── TCP connection → remote IP:443
```

This correlation will later be useful for detections.

---

# 9. Device Relationships

EF Core configures:

```text
Device 1 ──────── * ProcessEvents
Device 1 ──────── * NetworkEvents
```

The relationships use `Device.Id` as the foreign key.

Conceptually:

```text
Device A
   │
   ├── ProcessEvent 1
   ├── ProcessEvent 2
   ├── ProcessEvent 3
   │
   ├── NetworkEvent 1
   └── NetworkEvent 2
```

---

# 10. Foreign Key Integrity

The database enforces the relationship between telemetry and devices.

A telemetry record cannot reference a nonexistent Device.

The backend also performs an explicit existence check before creating telemetry records.

This gives us protection at two levels:

```text
API validation
      +
Database foreign-key integrity
```

The application should not rely on only one of these mechanisms.

---

# 11. Cascade Delete

The current relationships use cascade deletion.

Conceptually:

```text
Delete Device
     │
     ├── Delete ProcessEvents
     │
     └── Delete NetworkEvents
```

This is acceptable for the V1 development database.

However, production retention requirements may require a different strategy.

For a security telemetry system, automatically deleting potentially important forensic data when a device record is deleted may not always be desirable.

This should therefore be revisited before production use.

---

# 12. Entity Framework DbContext

The database is represented through:

```text
MersadDbContext
```

Current DbSets:

```csharp id="1r8m4x"
public DbSet<Device> Devices => Set<Device>();

public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();

public DbSet<NetworkEvent> NetworkEvents => Set<NetworkEvent>();
```

The context also defines important schema constraints.

For example:

```csharp id="5q9v2m"
modelBuilder.Entity<Device>()
    .HasIndex(d => d.AgentId)
    .IsUnique();
```

and the relationships:

```text
ProcessEvent.DeviceId → Device.Id

NetworkEvent.DeviceId → Device.Id
```

---

# 13. Database Migrations

Schema changes are managed through EF Core migrations.

For example, when network telemetry was introduced, the database schema changed to add `NetworkEvents`.

Migration:

```text
20261004130133_AddNetworkEvents
```

The migration is stored in source control.

This means the schema evolution is part of the project's version history.

Conceptually:

```text
Code change
    ↓
Entity model change
    ↓
Migration
    ↓
Database schema change
```

---

# 14. Current Data Flow Into the Database

The complete persistence flow is:

```text
Agent
  │
  ▼
TelemetryRequest
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
  ├──────────────┐
  ▼              ▼
ProcessEvent   NetworkEvent
  │              │
  └──────┬───────┘
         ▼
     PostgreSQL
```

This is one of the most important relationships in the V1 system.

---

# 15. Current Database Example

After running the Agent, the database can contain data conceptually similar to:

```text
Devices
------------------------------------------------
Id                  AgentId             Hostname
401ad2f3-...        0ca418f5-...        DESKTOP-GI264SM


ProcessEvents
------------------------------------------------
Id                  DeviceId            ProcessId
...                 401ad2f3-...       1234
...                 401ad2f3-...       5678
...


NetworkEvents
------------------------------------------------
Id                  DeviceId            ProcessId
...                 401ad2f3-...       1234
...                 401ad2f3-...       5678
...
```

Multiple telemetry events therefore point to the same `Device.Id`.

---

# 16. Why We Do Not Use Hostname as the Device Key

A hostname is descriptive rather than a reliable identity.

Two machines could potentially have the same hostname.

A hostname can also change.

Therefore:

```text
Hostname ≠ reliable identity
```

Instead:

```text
AgentId → Device.Id
```

is used for V1 device resolution.

---

# 17. Why We Do Not Use ProcessId as a Process Record Identity

Windows PIDs are temporary identifiers.

For example:

```text
powershell.exe
PID 1234
```

could terminate.

Later:

```text
chrome.exe
PID 1234
```

could exist.

Therefore:

```text
PID ≠ permanent process identity
```

The database gives every telemetry event its own GUID:

```text
ProcessEvent.Id
```

The PID remains telemetry information used for correlation.

---

# 18. Current Schema Limitations

The V1 database is intentionally minimal.

Current limitations include:

* No Alert table
* No Detection table
* No DetectionRule table
* No investigation/case model
* No user/account model
* No authentication/enrollment model
* No telemetry retention policy
* No partitioning
* No indexes optimized for large telemetry volumes
* Limited process metadata
* Limited network metadata
* No event deduplication
* No event sequence numbers

These become increasingly important as telemetry volume grows.

---

# 19. Expected Future Schema

The database will eventually need additional entities for the security workflow.

A possible future model is:

```text
Device
  │
  ├── ProcessEvents
  ├── NetworkEvents
  ├── FileEvents
  ├── DnsEvents
  │
  └── Alerts
         │
         ├── DetectionRule
         ├── Evidence
         └── Investigation
```

The exact schema should be designed when those requirements are implemented rather than prematurely added to V1.

---

# 20. Database Design Principles

### Use internal database identities

Telemetry references `Device.Id`.

### Enforce important invariants in the database

For example:

```text
AgentId must be unique.
```

### Keep telemetry models focused

Process and network events contain information relevant to those event types.

### Don't confuse telemetry identifiers with permanent identities

PID is telemetry data.

AgentId identifies an Agent installation.

Device.Id identifies the database record.

### Evolve the schema through migrations

Database changes should be version-controlled and reproducible.

### Avoid premature complexity

The V1 schema contains only the entities required for the current product flow.

---

# 21. V1 Database Summary

The V1 database establishes the foundation for Mersad's telemetry platform:

```text
                    Device
                      │
          ┌───────────┴───────────┐
          │                       │
          ▼                       ▼
   ProcessEvents            NetworkEvents
          │                       │
          └───────────┬───────────┘
                      │
                  PostgreSQL
```

The most important identity relationship is:

```text
AgentId
   ↓
Devices.AgentId
   ↓
Device.Id
   ↓
Telemetry.DeviceId
```

This allows the Agent-facing identity and backend database identity to remain separate while maintaining a clear relationship between a device and all of its telemetry.
