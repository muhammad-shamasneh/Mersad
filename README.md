# Mersad

**A Windows-first endpoint security monitoring project.**

Mersad collects endpoint process and network telemetry, evaluates activity against detection rules, and presents generated security alerts through a central dashboard.

The project is being developed incrementally, with an emphasis on understanding the full pipeline—from endpoint data collection to detection, storage, and investigation.

> **Status: Early development — not production-ready.** Mersad is a development project and should only be tested in controlled environments.

## Current capabilities

* **Endpoint agent:** A native C++ agent for Windows that collects process and IPv4 TCP network connection information.
* **Device registration:** Registers an endpoint with the backend using a locally persisted agent identity.
* **Telemetry API:** An ASP.NET Core API receives and processes telemetry.
* **Rule-based detection:** Evaluates telemetry against registered detection rules.
* **Persistent storage:** PostgreSQL stores device, event, and alert data.
* **Security dashboard:** A React and TypeScript dashboard displays alert counts, severity, descriptions, and alert records.
* **API integration:** The dashboard retrieves alerts from the backend and periodically refreshes its data.

## Architecture

```text
┌──────────────────────────────┐
│       Windows Endpoint       │
│                              │
│  Mersad.Agent (C++20)        │
│  ├── Process collector       │
│  ├── Network collector       │
│  ├── Device registration     │
│  └── Telemetry client        │
└──────────────┬───────────────┘
               │ HTTP / JSON
               ▼
┌──────────────────────────────┐
│       Mersad API             │
│       ASP.NET Core           │
│                              │
│  ├── Telemetry ingestion     │
│  ├── Detection engine        │
│  ├── Alert service           │
│  └── REST endpoints          │
└──────────────┬───────────────┘
               │ Entity Framework Core
               ▼
┌──────────────────────────────┐
│         PostgreSQL           │
│                              │
│  Devices, events, alerts     │
└──────────────────────────────┘

┌──────────────────────────────┐
│   React / TypeScript UI      │
│   Polls the alerts API       │
└──────────────┬───────────────┘
               │ HTTP
               └──────► Mersad API
```

### Telemetry-to-alert flow

1. The Windows agent initializes or loads its persistent identity.
2. The agent registers its device with the API.
3. It collects process and network connection information.
4. The agent submits telemetry batches to the API.
5. The backend processes and persists events, then evaluates applicable detection rules.
6. Generated alerts are stored in PostgreSQL.
7. The dashboard retrieves alerts from `GET /api/alerts` and refreshes periodically.

The current agent runs a collection cycle and exits. It is not yet a continuously running monitoring service.

## Technology stack

| Component            | Technology                 |
| -------------------- | -------------------------- |
| Endpoint agent       | C++20, CMake, Windows APIs |
| HTTP communication   | Windows WinHTTP            |
| Backend              | C#, ASP.NET Core, .NET 10  |
| Database access      | Entity Framework Core      |
| Database             | PostgreSQL 16              |
| Dashboard            | React, TypeScript, Vite    |
| Local infrastructure | Docker Compose             |

## Prerequisites

Install the following before starting the project:

* Windows for building and running the current endpoint agent.
* Git.
* CMake and Visual Studio with the C++ desktop development workload.
* .NET 10 SDK.
* Node.js and npm.
* Docker Desktop with Docker Compose.

The API and dashboard can be developed separately, but the complete local workflow requires the database and API to be available.

## Run locally

Run the following commands from the repository root unless otherwise specified.

### 1. Start PostgreSQL

```powershell
docker compose -f infrastructure/docker-compose.yml up -d
```

This starts the PostgreSQL container and publishes its database port on `localhost:5433`.

Check its status:

```powershell
docker compose -f infrastructure/docker-compose.yml ps
```

The Compose configuration uses development credentials. They are for local development only and must not be reused in a deployed environment.

### 2. Configure and initialize the database

The API reads its database connection string from configuration. The current development configuration targets:

* Host: `localhost`
* Port: `5433`
* Database: `mersad`

Check `backend/Mersad.Api/appsettings.json` and the development configuration before running the API. Keep credentials out of source control when using anything beyond a disposable local environment.

Install the Entity Framework Core CLI if it is not already installed:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
```

If the tool is already installed, update it to the matching version if necessary rather than installing it again.

Apply the database migrations:

```powershell
dotnet ef database update --project backend/Mersad.Api
```

This creates or updates the schema defined by the project's migrations. Run this after starting PostgreSQL and before starting the API.

### API key configuration

The API requires two separate secrets:

- `MersadSecurity__DashboardApiKey` — used by the dashboard.
- `MersadSecurity__AgentApiKey` — used by the agent.

Set both values in the environment of the API process before starting the backend. The dashboard must be configured with the dashboard key, and the agent uses `MERSAD_AGENT_API_KEY`.

For local development, these can be set as Windows user environment variables. Restart the API after changing its keys. Never commit real keys to source control or place them in documentation.
### 3. Start the backend API

```powershell
dotnet run --project backend/Mersad.Api --launch-profile http
```

The configured development API URL is:

`http://localhost:5121`

The API's OpenAPI document is available in development at:

`http://localhost:5121/openapi/v1.json`

Keep this terminal running while using the dashboard or agent.

### 4. Start the dashboard

Open another PowerShell terminal:

```powershell
cd dashboard/mersad-console
npm install
npm run dev
```

Open the local URL printed by Vite, normally:

`http://localhost:5173`

The dashboard expects the local API at `http://localhost:5121`.

### 5. Build the Windows agent

From the repository root:

```powershell
cmake -S agent/Mersad.Agent -B agent/Mersad.Agent/build
cmake --build agent/Mersad.Agent/build --config Debug
```

Run the executable produced by the build. With the standard Visual Studio multi-configuration generator, it is typically located at:

`agent/Mersad.Agent/build/Debug/Mersad.Agent.exe`

Run the agent only in an environment where you have permission to collect endpoint telemetry.

The agent registers the device, collects process and network information, sends the telemetry batches, and exits. The API and database must be running first.

The agent persists its identity in `Mersad.AgentId` relative to its working directory. The file is ignored by Git; keep it stable between runs when you want to reuse the same local identity.

## Detection rules

The current backend registers these rules:

| Rule ID    | Detection                                                         | Severity |
| ---------- | ----------------------------------------------------------------- | -------- |
| `PROC-001` | Suspicious PowerShell execution                                   | Medium   |
| `NET-001`  | Network connection to a configured suspicious remote port | High     |
| `CORR-001` | Correlated PowerShell execution and suspicious network activity   | High     |

The suspicious-port policy is configured in
`backend/Mersad.Api/appsettings.json` under the `Detection` section.
The current configured ports are `4444`, `1337`, and `31337`.
The three detection rules can also be enabled or disabled through
configuration. Invalid or duplicate port values cause startup
validation to fail. An empty port list is valid and disables
port-based matches.

These settings currently apply to backend detection only; Mersad
does not yet have a shared configuration system spanning all components.

`CORR-001` correlates recent persisted process and network events across ingestion requests when the device, process ID, suspicious remote port, and five-minute time window match. Composite indexes on `(DeviceId, Timestamp)` support per-device telemetry lookups. For SQLite compatibility, the current implementation still filters timestamps in application code after loading that device's events, so correlation performance may degrade as telemetry grows.

The alert service checks for existing alerts with the same rule and evidence references. This reduces duplicate alerts during sequential processing, but the check is not concurrency-safe and is not a database-enforced uniqueness guarantee.
These are demonstration detection rules, not a comprehensive threat-detection engine. A rule match is an alert-worthy signal, not proof that a device has been compromised.

## Repository structure

```text
Mersad/
├── agent/
│   └── Mersad.Agent/
│       ├── include/          # C++ interfaces and declarations
│       ├── src/              # Agent implementation
│       └── CMakeLists.txt
├── backend/
│   └── Mersad.Api/
│       ├── Alerts/           # Alert services
│       ├── Controllers/      # HTTP API endpoints
│       ├── Data/              # EF Core database context
│       ├── Detection/         # Detection engine and rules
│       ├── Migrations/        # Database schema migrations
│       └── Models/            # API and persistence models
├── dashboard/
│   └── mersad-console/        # React and TypeScript application
├── infrastructure/
│   └── docker-compose.yml     # Local PostgreSQL environment
├── docs/
│   └── documentation_v1/      # Detailed V1 documentation
├── .gitignore
└── README.md
```

## Known limitations

Mersad is an early-stage project. Important limitations include:

* The agent performs a one-shot collection cycle rather than continuous monitoring.
* Detection is rule-based and limited to the currently implemented rules.
* Detection settings are configurable through backend configuration, but dynamic reload and a shared cross-component configuration system are not implemented.
* Dashboard and agent API endpoints use separate Bearer API keys. Production-grade identity management, transport security, and deployment hardening remain incomplete.
* The project does not yet provide a production-grade investigation workflow or implemented AI-generated alert explanations.
* Telemetry collection, reliability, scalability, and error handling need further development and testing.

Do not deploy this version as a production endpoint detection and response (EDR) system.

## Roadmap

Planned development areas include:

* A shared configuration system spanning components, with clear ownership, validation, and customer-specific policy support.
* More reliable and continuous endpoint monitoring.
* Better telemetry validation, delivery handling, and diagnostics.
* Richer alert investigation and dashboard workflows.
* Expanded automated tests and build validation.
* Stronger authentication, authorization, and secure deployment practices.
* Improved documentation and reproducible development workflows.

## Development and documentation

Detailed V1 documentation is maintained in [`docs/documentation_v1/`](docs/documentation_v1/).

When contributing, keep the documentation aligned with behavior that is actually implemented. Distinguish current capabilities from planned features, and avoid committing generated build output, local identity files, or secrets.

## Security notice

Mersad is an evolving security engineering project intended for authorized development and testing. It is not currently a substitute for a supported commercial security product.

## Windows local demo launcher

Requirements: Docker Desktop, the .NET SDK, the Entity Framework CLI, Node.js/npm, and the Mersad API key environment variables configured for your Windows user.

Start the local demo from PowerShell:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Mersad\scripts\Start-Mersad.ps1"

Stop the API and dashboard:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Mersad\scripts\Stop-Mersad.ps1"

The launcher writes logs to %LOCALAPPDATA%\Mersad\logs. Stopping the demo leaves PostgreSQL and its data intact. This is a local development demo, not a production deployment.
