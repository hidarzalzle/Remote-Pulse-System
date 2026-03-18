# RemotePulse Technical Documentation

## 1. Document Purpose

This document is a comprehensive technical reference for the **RemotePulse** solution. It is intended to help developers, reviewers, DevOps engineers, and stakeholders understand how the system is structured, how its projects collaborate, how data flows through the platform, and how to run, troubleshoot, and extend the solution.

RemotePulse is a cloud-native, portfolio-style distributed .NET system that demonstrates:

- service orchestration with **.NET Aspire**,
- API and background processing with **ASP.NET Core**, **Minimal APIs**, and **Worker Services**,
- realtime browser updates with **SignalR**,
- internal service-to-service communication with **gRPC**,
- relational persistence with **Entity Framework Core**,
- observability with **OpenTelemetry**, and
- a browser dashboard built with **Blazor WebAssembly**.

---

## 2. High-Level System Overview

At a high level, RemotePulse simulates an operational monitoring platform where a background ingestion process continuously generates “pulse” telemetry, a backend API validates and persists that telemetry, and a frontend dashboard visualizes the live state of the system in realtime.

### 2.1 Primary goals of the solution

The solution is designed to demonstrate several technical qualities:

1. **Distributed architecture** with clearly separated responsibilities.
2. **Realtime user experience** through push-based updates.
3. **Modern .NET cloud-native composition** using Aspire.
4. **Portable local development** via both Aspire and Docker Compose.
5. **Operational readiness** through health checks, telemetry, structured logging, and standardized defaults.

### 2.2 Major runtime components

The solution contains the following runtime projects:

- **AppHost**: Orchestrates the solution using .NET Aspire.
- **Backend.Api**: Hosts HTTP APIs, gRPC ingestion, SignalR hub, EF Core persistence, and health endpoints.
- **Worker.Ingestion**: Simulates incoming telemetry and pushes data into the backend.
- **Frontend.Wasm**: Provides the browser dashboard built with Blazor WebAssembly.
- **Frontend.Host**: Serves the WebAssembly client as an ASP.NET Core host.
- **ServiceDefaults**: Centralizes cross-cutting operational configuration such as telemetry and health registration.

---

## 3. Repository and Solution Structure

```text
RemotePulse.sln
README.md
docker-compose.yml
docs/
  TECHNICAL_DOCUMENTATION.md
src/
  AppHost/
  Backend.Api/
  Frontend.Host/
  Frontend.Wasm/
  ServiceDefaults/
  Worker.Ingestion/
```

### 3.1 Solution-level assets

- `RemotePulse.sln` binds the projects together into one buildable solution.
- `README.md` provides a concise overview and getting-started guidance.
- `docker-compose.yml` offers a containerized local orchestration path.
- `docs/TECHNICAL_DOCUMENTATION.md` provides this deep-dive technical reference.

---

## 4. Architecture Overview

### 4.1 Architectural style

RemotePulse follows a **distributed, service-oriented architecture** with lightweight boundaries between orchestration, API, background processing, and UI responsibilities.

It is not a large microservices platform, but it intentionally demonstrates the same kinds of concerns seen in production distributed systems:

- service discovery and endpoint wiring,
- internal RPC and external HTTP APIs,
- persistence and schema management,
- browser-facing realtime communication,
- observability and diagnostics,
- alternate local orchestration strategies.

### 4.2 Architectural patterns used

The solution uses several patterns:

- **Vertical slice organization** in the backend, where feature code is grouped by capability instead of by technical layer only.
- **Minimal API** endpoint definitions for concise HTTP surface area.
- **Strongly typed IDs** for domain-level safety.
- **Shared operational defaults** through a dedicated library.
- **Push-based UI updates** via SignalR.
- **Transport fallback** in the worker, where gRPC is attempted first and HTTP is used when selected gRPC failures occur.

### 4.3 Logical component diagram

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

---

## 5. Project-by-Project Documentation

## 5.1 AppHost

### Purpose

`src/AppHost` is the orchestration entry point for the Aspire-based local development experience. It defines which services should run together and how those services depend on one another.

### Responsibilities

- Starts a PostgreSQL resource.
- Creates the `remotepulse` database resource.
- Registers the backend API project.
- Registers the ingestion worker.
- Registers the frontend host.
- Establishes startup ordering so dependent services wait until required resources are ready.

### Key implementation details

The AppHost:

- creates PostgreSQL using `AddPostgres("postgres")`,
- enables pgAdmin with `WithPgAdmin()`,
- exposes a logical database called `remotepulse`,
- ensures `backend-api` waits for the database,
- ensures `worker-ingestion` waits for the backend,
- ensures `frontend` waits for the backend and exposes external HTTP endpoints.

### Why this matters

This project gives the repository a cloud-native local orchestration story without requiring the developer to start every process manually. It also centralizes the dependency graph, which makes the system easier to reason about and demo.

---

## 5.2 ServiceDefaults

### Purpose

`src/ServiceDefaults` is a shared library that standardizes operational infrastructure concerns across services.

### Responsibilities

- Registers OpenTelemetry tracing.
- Registers OpenTelemetry metrics.
- Enables runtime instrumentation.
- Registers health checks.
- Registers service discovery support through an extension point.
- Provides a helper for mapping a conventional `/health` endpoint.

### Important design note

The current `AddServiceDiscovery()` implementation is intentionally a no-op placeholder. This keeps the solution buildable even if a richer service discovery implementation from another library is not present.

### Why this matters

Without a shared defaults layer, each service would need to duplicate health, telemetry, and infrastructure registration. This library reduces drift and keeps service startup consistent.

---

## 5.3 Backend.Api

### Purpose

`src/Backend.Api` is the central runtime component of the system. It acts as the system of record, ingress point, realtime broadcaster, and health summary provider.

### Responsibilities

- Exposes HTTP endpoints for pulse retrieval and ingestion.
- Exposes a gRPC ingestion service.
- Broadcasts newly ingested pulses over SignalR.
- Persists pulse data with EF Core.
- Migrates the database automatically at startup.
- Publishes an aggregated health summary endpoint.
- Enables API exploration via OpenAPI and Swagger UI.

### Startup behavior

At startup, the backend:

1. applies shared service defaults,
2. configures Kestrel to support both HTTP/1.1 and HTTP/2 on port `5001`,
3. enables OpenAPI and Swagger UI,
4. resolves the database provider,
5. configures CORS for frontend access,
6. registers Hybrid Cache, SignalR, and gRPC,
7. maps endpoints,
8. executes pending EF Core migrations.

### Database provider strategy

The backend supports two persistence modes:

- **Primary path**: PostgreSQL when `ConnectionStrings:remotepulse` is available.
- **Fallback path**: SQLite for local development when PostgreSQL is not configured.

This dual-mode strategy makes the app easier to run in different environments while still demonstrating a production-oriented database path.

### CORS policy

A named CORS policy called `Frontend` is registered and allows expected frontend local origins. Credentials are allowed so the browser client can establish SignalR connections correctly when hosted from approved origins.

---

## 5.4 Worker.Ingestion

### Purpose

`src/Worker.Ingestion` simulates an external telemetry producer. It continuously generates pulse events and submits them to the backend.

### Responsibilities

- Runs as a background service.
- Generates sample pulse values every three seconds.
- Sends data to the backend using gRPC.
- Falls back to HTTP ingestion if gRPC is temporarily unavailable.
- Emits logs and activities for observability.

### Data generation behavior

Each generated pulse contains:

- a GUID v7 identifier,
- the current UTC timestamp,
- a random BPM value from `55` to `124` inclusive (because `Random.Next(55, 125)` uses an exclusive upper bound),
- a source label of `worker-simulator`.

### Resilience behavior

The worker intentionally catches a specific set of gRPC errors:

- `Unavailable`,
- `Internal`,
- `DeadlineExceeded`.

When those occur, it logs a warning and falls back to HTTP ingestion through a typed `HttpClient`.

### Why this matters

This project demonstrates internal service-to-service communication, retry posture, transport flexibility, and long-running background processing in a distributed .NET application.

---

## 5.5 Frontend.Wasm

### Purpose

`src/Frontend.Wasm` contains the browser dashboard implemented with Blazor WebAssembly.

### Responsibilities

- Loads initial backend state from HTTP endpoints.
- Establishes a SignalR connection to receive live pulse updates.
- Maintains a rolling set of recent pulses in browser memory.
- Derives dashboard metrics such as total count, last BPM, rolling average, trend, source distribution, and sparkline data.
- Renders a polished operations-style dashboard.

### UI model

The main page combines two data acquisition models:

1. **HTTP initialization** for current state:
   - `/health/summary`
   - `/api/pulses/latest`
2. **SignalR subscription** for incremental updates:
   - `/hubs/pulse`

This hybrid model ensures the UI can render meaningful state immediately and then remain live as new events arrive.

### Connection lifecycle handling

The frontend reacts to connection state changes and surfaces states such as:

- `Connecting`
- `Live`
- `Reconnecting`
- `Offline`

This is important because operational dashboards must make connectivity state visible, not only data values.

---

## 5.6 Frontend.Host

### Purpose

`src/Frontend.Host` is a lightweight ASP.NET Core host used to serve the Blazor WebAssembly application.

### Responsibilities

- serves framework files,
- serves static files,
- configures production error handling and HSTS,
- maps all unmatched routes to `index.html` for client-side routing support.

### Why this exists

Blazor WebAssembly applications can be hosted as static assets. This host provides a simple, standard server process that works well with Aspire and Docker Compose orchestration.

---

## 6. Data Flow End-to-End

This section describes the primary functional flow of the system.

### 6.1 Ingestion flow

1. `Worker.Ingestion` creates a new pulse object.
2. The worker attempts to send the pulse via gRPC to `Backend.Api`.
3. The backend validates the payload.
4. If valid, the backend stores the pulse in the database.
5. The backend broadcasts the pulse to all connected SignalR clients.
6. The frontend receives the event and refreshes the visible dashboard state.

### 6.2 Fallback flow

1. If gRPC submission fails with selected transient-style errors,
2. the worker issues an HTTP `POST` to `/api/pulses/ingest`,
3. the backend performs the same validation and persistence flow,
4. the same SignalR broadcast occurs.

### 6.3 Read flow for initial page load

1. The browser loads the WebAssembly app from `Frontend.Host`.
2. The dashboard requests `/health/summary`.
3. The dashboard requests `/api/pulses/latest`.
4. The dashboard calculates derived metrics locally.
5. The dashboard starts the SignalR stream for subsequent live updates.

---

## 7. Backend API Surface

## 7.1 HTTP endpoints

### `GET /api/pulses/latest`

Returns up to the 50 most recent pulse records.

#### Behavior notes

- Uses `AsNoTracking()` for read efficiency.
- Contains a SQLite-specific fallback path because provider translation for ordering on `DateTimeOffset` is treated differently.
- Returns `PulseRecordDto` objects.

### `POST /api/pulses/ingest`

Accepts a pulse payload and, if valid:

- persists it,
- broadcasts it through SignalR,
- returns `202 Accepted` with the submitted payload.

#### Validation rules

- BPM must be within `20` to `260` inclusive.
- Invalid payloads return `400 Bad Request`.

### `GET /health/summary`

Returns an aggregated health-oriented summary built from recent pulse data.

#### Returned fields

- `TotalRecords`
- `LastObservedAtUtc`
- `AverageBpm`

#### Caching behavior

This endpoint uses `HybridCache` and stores the summary under the cache key `health-summary`.

### `GET /health`

This conventional health endpoint is mapped through shared service defaults.

---

## 7.2 gRPC service

The backend exposes a gRPC service named `PulseIngestionGrpcService`, generated from `Protos/pulse.proto`.

### Purpose

It provides an internal service-to-service ingestion path optimized for backend communication.

### Method behavior

The `IngestPulse` method:

- converts the gRPC request into the shared DTO shape,
- validates the BPM range,
- stores the entity,
- broadcasts the event via SignalR,
- returns a reply indicating whether the pulse was accepted.

### Design observation

The HTTP and gRPC ingestion paths intentionally share similar validation and persistence logic. This keeps behavior consistent regardless of transport.

---

## 7.3 SignalR hub

The backend exposes a SignalR hub at `/hubs/pulse`.

### Current behavior

The hub itself is a marker hub with no server methods. The primary use case is server-to-client broadcasting from backend ingestion handlers.

### Event contract

Clients listen for the event name:

- `pulse.received`

The event payload shape matches the frontend `PulseRecordMessage` record.

---

## 8. Domain Model and Persistence

## 8.1 Domain objects

### `PulseRecordDto`

A transport-facing immutable record containing:

- `Id`
- `ObservedAtUtc`
- `Bpm`
- `Source`

### `PulseEntity`

A persistence-facing entity representing a stored pulse record.

### `PulseId`

A strongly typed identifier wrapping a `Guid`. This avoids using raw primitive IDs throughout the code and improves type safety.

### `Result<T>`

A lightweight success/failure wrapper used to return validation outcomes without relying on exceptions for normal control flow.

---

## 8.2 Database model

The pulse entity is mapped to the `pulse_records` table.

### Configured schema behavior

- Primary key: `Id`
- `Id` uses a conversion between `PulseId` and `Guid`
- `ObservedAtUtc` is required
- `Bpm` is required
- `Source` is required and limited to 120 characters
- index on `ObservedAtUtc`

### Why this schema is appropriate

The table is optimized for append-heavy telemetry ingestion and time-ordered reads. Indexing by `ObservedAtUtc` supports the primary access pattern used by the dashboard and health summary.

---

## 8.3 DbContext behavior

`PulseDbContext`:

- exposes `DbSet<PulseEntity>` as `Pulses`,
- applies all entity configurations from the assembly,
- conditionally configures Npgsql migrations support when the active provider is PostgreSQL.

### Migration strategy

On application startup, the backend creates a scope, resolves the DbContext, and executes `Database.MigrateAsync()`. This means schema creation and migration happen automatically when the backend starts.

---

## 9. Frontend Behavior in Detail

## 9.1 Initial load sequence

On initialization, the dashboard:

1. subscribes to pulse and connection events from `PulseStreamClient`,
2. requests the health summary,
3. requests the latest pulse list,
4. hydrates local state,
5. calculates derived metrics,
6. starts the SignalR connection.

## 9.2 Local derived state

The page calculates several values in browser memory:

- total record count,
- latest BPM,
- minimum BPM,
- maximum BPM,
- rolling average BPM,
- trend label,
- signal quality label,
- source breakdown percentages,
- sparkline point coordinates.

### Trend logic

The displayed trend is:

- `Rising` when latest BPM is at least 8 above the average,
- `Falling` when latest BPM is at least 8 below the average,
- otherwise `Stable`.

### Signal quality logic

The displayed quality is derived from the range between minimum and maximum tracked BPM values:

- no data => `Awaiting data`
- range `<= 12` => `Steady`
- range `<= 25` => `Elevated variance`
- otherwise `High variance`

### Source activity logic

The UI groups the current in-memory pulse window by source and calculates integer percentages for each source.

### Sparkline logic

The chart is rendered entirely in SVG by calculating normalized X/Y coordinates from the rolling pulse window. No external charting library is required.

---

## 9.3 SignalR client service

`PulseStreamClient` encapsulates realtime connection management.

### Responsibilities

- constructs the hub connection,
- subscribes to `pulse.received`,
- enables automatic reconnect,
- raises frontend-facing events for pulse reception,
- translates low-level SignalR states into simple UI-friendly labels.

### Operational value

By isolating realtime connection logic in a dedicated service, the page component remains focused on presentation and derived metrics instead of transport internals.

---

## 10. Observability and Operational Concerns

## 10.1 OpenTelemetry

Shared service defaults enable OpenTelemetry for all participating services.

### Trace instrumentation

Tracing includes:

- ASP.NET Core instrumentation,
- HttpClient instrumentation,
- OTLP export.

### Metrics instrumentation

Metrics include:

- ASP.NET Core instrumentation,
- HttpClient instrumentation,
- runtime instrumentation,
- OTLP export.

## 10.2 Activity sources

Custom `ActivitySource` instances are used in both the backend and the ingestion worker.

### Backend activity names

- `pulse.ingest.http`
- `pulse.ingest.grpc`

### Worker activity names

- `ingestion.push`

These activities make it easier to trace ingestion operations through the system.

## 10.3 Logging

The solution uses structured `ILogger` logging to describe:

- worker startup,
- gRPC fallback conditions,
- successful pulse ingestion,
- validation failures,
- SignalR reconnect and disconnect events,
- health summary cache rebuilds.

## 10.4 Health checks

The shared defaults library maps `/health`, which provides a standard health endpoint for orchestration systems and diagnostics tooling.

---

## 11. Local Development and Execution Paths

## 11.1 Aspire-based execution

The recommended orchestration path is to run the AppHost:

```bash
dotnet run --project src/AppHost/AppHost.csproj
```

### What this starts

- PostgreSQL
- pgAdmin
- Backend API
- Worker Ingestion
- Frontend Host

### Benefits of this path

- integrated local orchestration,
- dependency-aware startup ordering,
- Aspire dashboard visibility,
- simpler service management during demos and development.

---

## 11.2 Docker Compose execution

An alternate orchestration path is:

```bash
docker compose up --build
```

### What Compose starts

- PostgreSQL container
- backend API container
- worker ingestion container
- frontend host container

### Key Compose characteristics

- the services use the .NET nightly SDK image,
- the repository is bind-mounted into containers,
- restore/build steps are performed within the container startup commands,
- health checks gate dependencies.

### Important operational note

Docker Compose and Aspire are separate local orchestration mechanisms. Running one does not automatically register resources in the other’s dashboard or lifecycle.

---

## 11.3 Common local endpoints

When running locally, the main default endpoints are:

- Backend API: `http://localhost:5001`
- Frontend host: `http://localhost:5003`
- PostgreSQL: `localhost:5432`

---

## 12. Configuration Reference

## 12.1 Backend configuration areas

Important backend configuration includes:

- `ConnectionStrings:remotepulse`
- `Cors:AllowedOrigins`

### Behavior summary

- If `ConnectionStrings:remotepulse` exists, PostgreSQL is used.
- If it does not exist, SQLite is used.
- If `Cors:AllowedOrigins` is absent, local development defaults are applied.

## 12.2 Worker configuration areas

The worker looks for backend service endpoints in configuration using:

- `services:backend-api:https:0`
- `services:backend-api:http:0`

If those are not present, it falls back to `http://localhost:5001`.

## 12.3 Frontend configuration areas

The WebAssembly app uses `BackendBaseUrl` and falls back to `http://localhost:5001` when not supplied.

---

## 13. Security and Production Considerations

This repository is a demonstration system, but several production-oriented considerations can still be identified.

### Current strengths

- CORS is explicitly configured.
- Database access is provider-configurable.
- Health endpoints are present.
- Telemetry export is enabled.
- Realtime connection state is surfaced to users.

### Areas to improve for production deployment

1. **Authentication and authorization** are not currently present.
2. **Input validation** is minimal and could be expanded.
3. **Hybrid cache invalidation** is not explicit after ingest operations.
4. **Secrets handling** should be environment- or vault-based rather than local defaults.
5. **Rate limiting** could protect ingestion endpoints.
6. **Retention and archiving** policies may be needed for long-running telemetry storage.
7. **Error contracts** could be formalized more consistently across HTTP and gRPC.
8. **SignalR scale-out** considerations would be needed for multi-instance deployments.

---

## 14. Extension and Maintenance Guidance

## 14.1 How to add a new backend feature

A practical extension path would be:

1. create a new feature folder under `src/Backend.Api/Features`,
2. add DTOs and endpoint mappings within that feature,
3. add persistence entities and configuration if storage is needed,
4. register the feature in `Program.cs`,
5. update frontend consumers if the feature is user-facing.

## 14.2 How to add a new dashboard metric

1. determine whether the metric should be backend-generated or frontend-derived,
2. add or extend the necessary backend endpoint if server-side aggregation is required,
3. update the page state model in `Index.razor`,
4. recalculate the derived state in `RefreshDerivedState()`,
5. add the corresponding visual presentation block.

## 14.3 How to replace the ingestion simulator

If a real device feed or external source is introduced, the current worker can be replaced or extended to:

- consume from a message broker,
- read from IoT devices,
- poll an upstream API,
- process batched files.

The rest of the architecture can remain largely unchanged if the backend contracts stay stable.

---

## 15. Troubleshooting Guide

## 15.1 Frontend shows no live updates

Check the following:

- the backend is running,
- the browser can reach `/hubs/pulse`,
- CORS origins include the frontend host origin,
- the worker is actively sending events,
- the connection indicator in the UI is not `Offline`.

## 15.2 Data is not persisting

Check the following:

- the database connection string is correct,
- PostgreSQL is reachable if that mode is intended,
- SQLite fallback files are writable if using fallback mode,
- backend startup logs do not show migration failures,
- validation is not rejecting payloads due to BPM range issues.

## 15.3 Worker is not ingesting via gRPC

Check the following:

- backend port `5001` is reachable,
- HTTP/2 support is enabled,
- worker configuration resolves the correct backend endpoint,
- logs indicate whether fallback to HTTP is occurring.

## 15.4 Health summary looks stale

Because `/health/summary` uses `HybridCache`, stale values can occur if the cache remains warm and no invalidation strategy is applied after writes. If exact real-time accuracy is required for this endpoint, explicit cache invalidation or a shorter cache policy should be introduced.

---

## 16. Technology Stack Summary

### Languages and frameworks

- C#
- .NET 10
- ASP.NET Core
- Blazor WebAssembly
- .NET Aspire

### Data and communication

- Entity Framework Core 10
- PostgreSQL
- SQLite fallback
- gRPC
- SignalR
- REST-style Minimal APIs

### Operations and diagnostics

- OpenTelemetry
- OTLP exporter
- ASP.NET Core health checks
- structured logging

### Hosting and orchestration

- .NET Aspire AppHost
- Docker Compose
- ASP.NET Core static hosting for WASM

---

## 17. Conclusion

RemotePulse is a compact but well-structured demonstration of a modern .NET distributed application. Its value is not only in the visible dashboard, but in how the solution combines orchestration, persistence, internal RPC, browser realtime updates, standardized operations, and multiple local run paths into one coherent reference architecture.

For developers reviewing the repository, the most important takeaway is that each project has a focused responsibility:

- **AppHost** orchestrates,
- **ServiceDefaults** standardizes,
- **Backend.Api** receives, stores, and broadcasts,
- **Worker.Ingestion** simulates upstream input,
- **Frontend.Wasm** visualizes live state,
- **Frontend.Host** serves the client.

Together, these projects form a practical, understandable baseline for showcasing cloud-native .NET application design.
