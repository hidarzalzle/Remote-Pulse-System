# RemotePulse (.NET 10 Cloud-Native Reference)

RemotePulse is a portfolio-grade distributed .NET application that demonstrates a modern cloud-native architecture for collecting, storing, and visualizing live pulse telemetry.

It combines:

- **.NET Aspire** for local orchestration,
- **ASP.NET Core Minimal APIs** for HTTP endpoints,
- **gRPC** for internal ingestion,
- **SignalR** for realtime browser updates,
- **Entity Framework Core** for persistence,
- **Blazor WebAssembly** for the dashboard UI,
- **OpenTelemetry** for observability.

## Detailed documentation

For a full technical deep dive covering architecture, data flow, API contracts, persistence, observability, configuration, troubleshooting, and extension guidance, see:

- [`docs/TECHNICAL_DOCUMENTATION.md`](docs/TECHNICAL_DOCUMENTATION.md)

## What the system does

RemotePulse simulates a telemetry platform where:

1. a background worker continuously generates sample pulse readings,
2. the backend API accepts those readings through gRPC or HTTP,
3. the readings are stored in a relational database,
4. the backend broadcasts new readings to connected clients via SignalR,
5. a Blazor WebAssembly dashboard renders live operational metrics in realtime.

## Architecture at a glance

```text
+-------------------+        gRPC / HTTP         +----------------------+
| Worker.Ingestion  | -------------------------> | Backend.Api          |
| pulse simulator   |                            | ingest + persist     |
+-------------------+                            | health + SignalR     |
                                                 +----------+-----------+
                                                            |
                                                            | SignalR
                                                            v
                                                 +----------------------+
                                                 | Frontend.Wasm        |
                                                 | realtime dashboard   |
                                                 +----------------------+
                                                            ^
                                                            |
                                                  served by |
                                                            |
                                                 +----------------------+
                                                 | Frontend.Host        |
                                                 | static host          |
                                                 +----------------------+

+----------------------+
| AppHost              |
| Aspire orchestrator  |
+----------------------+
```

## Why this architecture

- **Clear bounded ownership**: backend features are organized by capability such as pulse ingestion and health summary.
- **Operational consistency**: shared defaults centralize health checks, telemetry, and common setup.
- **Realtime visibility**: SignalR keeps the dashboard synchronized with newly ingested events.
- **Transport flexibility**: the worker prefers gRPC and falls back to HTTP when selected transient failures occur.
- **Portable development workflows**: the system can be started via either Aspire or Docker Compose.
- **Demonstration value**: the repository showcases multiple production-relevant cloud-native patterns in a compact solution.

## Solution layout

```text
src/
  AppHost/                 # .NET Aspire orchestration entry point
  ServiceDefaults/         # shared telemetry/health/service defaults
  Backend.Api/             # minimal API + EF Core + SignalR + gRPC backend
  Worker.Ingestion/        # background ingestion simulator
  Frontend.Wasm/           # Blazor WebAssembly dashboard
  Frontend.Host/           # ASP.NET Core host for serving the WASM app
```

## Project guide

| Project | Purpose | Key responsibilities |
|---|---|---|
| `AppHost` | Local orchestration | Starts PostgreSQL, backend, worker, frontend host, and Aspire resources. |
| `ServiceDefaults` | Shared operational wiring | Registers OpenTelemetry, health checks, and common service defaults. |
| `Backend.Api` | Core backend service | Hosts HTTP endpoints, gRPC ingestion, SignalR hub, caching, and persistence. |
| `Worker.Ingestion` | Telemetry producer | Generates pulse data every few seconds and sends it to the backend. |
| `Frontend.Wasm` | Browser client | Renders the dashboard and listens for live updates through SignalR. |
| `Frontend.Host` | Static hosting layer | Serves the Blazor WebAssembly application in local orchestrated runs. |

## Key implementation highlights

- **Framework target**: .NET 10 across all projects.
- **API style**: ASP.NET Core Minimal APIs with OpenAPI 3.1 metadata.
- **Realtime layer**: SignalR broadcasts `pulse.received` events to the browser.
- **Internal RPC**: gRPC ingestion service used by the worker.
- **Persistence**: EF Core 10 with PostgreSQL as the primary database provider.
- **Development fallback**: SQLite is used by the backend when PostgreSQL is not configured.
- **Caching**: `HybridCache` is used for `/health/summary`.
- **Patterns used**:
  - vertical slice feature organization,
  - strongly typed IDs (`PulseId`),
  - `Result<T>` for validation flow,
  - structured logging and `ActivitySource` instrumentation,
  - cancellation token propagation.

## Main runtime flows

### Ingestion flow

1. `Worker.Ingestion` creates a new pulse reading.
2. The worker sends it to `Backend.Api` via gRPC.
3. If gRPC is unavailable, the worker falls back to HTTP ingestion.
4. The backend validates and stores the pulse.
5. The backend broadcasts the new pulse through SignalR.
6. The frontend updates visible metrics and the live feed immediately.

### Initial dashboard load

1. The browser loads the app from `Frontend.Host`.
2. The UI requests `/health/summary`.
3. The UI requests `/api/pulses/latest`.
4. The UI opens a SignalR connection to `/hubs/pulse`.
5. New events stream into the dashboard as they arrive.

## API and protocol overview

### HTTP endpoints

- `GET /api/pulses/latest` — returns the latest pulse records.
- `POST /api/pulses/ingest` — accepts pulse ingestion over HTTP.
- `GET /health/summary` — returns cached aggregate system metrics.
- `GET /health` — standard health check endpoint.
- `GET /openapi/v1.json` — OpenAPI document.
- Swagger UI is served by the backend for API exploration.

### gRPC

- Backend exposes a pulse ingestion gRPC service defined in `src/Backend.Api/Protos/pulse.proto`.

### SignalR

- Hub endpoint: `/hubs/pulse`
- Event name: `pulse.received`

## Persistence model

Pulse records are stored by the backend using EF Core.

- Primary database: **PostgreSQL**
- Development fallback: **SQLite**
- Main entity fields:
  - `Id`
  - `ObservedAtUtc`
  - `Bpm`
  - `Source`

The database is migrated automatically on backend startup.

## Running the solution

## Option 1: Run with Aspire (recommended)

From the repository root:

```bash
dotnet run --project src/AppHost/AppHost.csproj
```

### Aspire starts

- PostgreSQL
- pgAdmin
- Backend API
- Worker Ingestion
- Frontend Host

### What to do next

- Open the Aspire dashboard URL shown in the terminal.
- Launch the `frontend` endpoint from the dashboard.
- Use the `backend-api` resource to inspect logs, health, and traces.

## Option 2: Run with Docker Compose

```bash
docker compose up --build
```

### Docker Compose starts

- PostgreSQL
- Backend API
- Worker Ingestion
- Frontend Host

> Docker Compose and Aspire are separate local orchestration flows. Running Docker Compose does **not** automatically surface those resources in the Aspire dashboard.

## Default local endpoints

- Backend API: `http://localhost:5001`
- Frontend host: `http://localhost:5003`
- PostgreSQL: `localhost:5432`

## Configuration notes

### Backend

- `ConnectionStrings:remotepulse`
- `Cors:AllowedOrigins`

Behavior:
- if `ConnectionStrings:remotepulse` is present, PostgreSQL is used,
- otherwise the backend falls back to SQLite.

### Worker

The worker resolves backend addresses from:

- `services:backend-api:https:0`
- `services:backend-api:http:0`

If neither is present, it falls back to `http://localhost:5001`.

### Frontend

The Blazor WebAssembly client uses:

- `BackendBaseUrl`

If not set, it also falls back to `http://localhost:5001`.

## Observability

Shared service defaults enable:

- OpenTelemetry tracing,
- OpenTelemetry metrics,
- runtime instrumentation,
- OTLP export,
- ASP.NET Core health checks.

Custom `ActivitySource` instrumentation is used in both the backend and the ingestion worker to help trace pulse submission paths.

## Troubleshooting quick tips

- If the dashboard is offline, check backend reachability and CORS configuration.
- If no data is appearing, verify that the worker is running and that the backend is accepting ingestion.
- If gRPC fails, inspect worker logs to confirm whether HTTP fallback is being used.
- If health summary data appears stale, remember that `/health/summary` is cached via `HybridCache`.

## Repository notes

- This repository is intentionally optimized as a portfolio/reference system for demonstrating architecture and composition patterns.
- The presence of both Aspire and Docker Compose is deliberate so the system can be explored in more than one local orchestration mode.
- The backend directory currently includes local SQLite database files used by the fallback development mode.

## License

This repository includes a `LICENSE` file at the root. Review it for usage terms.
