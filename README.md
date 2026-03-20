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

## Deploying to the cloud

The current repository is already close to being deployable on managed container platforms, but a production-style cloud deployment should tighten configuration, networking, security, and operations.

### 1. Things that should be updated in the code to be cloud ready

1. **Move all environment-specific settings to managed configuration**
   
   - Keep connection strings, CORS origins, OTLP endpoints, and public base URLs out of local-only config files.
   - Read them from Azure App Configuration / Key Vault or AWS Systems Manager Parameter Store / Secrets Manager.
   - Ensure each service supports environment-variable overrides for every runtime setting used in production.
2. **Make the frontend base URL and backend public URL explicit**
   
   - Set `BackendBaseUrl` to the public backend hostname that clients use in Azure or AWS.
   - Make sure SignalR and API calls resolve through the same public endpoint strategy used by the chosen ingress or load balancer.
3. **Replace local development fallbacks in production environments**
   
   - Do not rely on SQLite in cloud deployments.
   - Force PostgreSQL for hosted environments and fail fast when the production database connection string is missing.
4. **Harden CORS and networking rules**
   
   - Restrict `Cors:AllowedOrigins` to the deployed frontend domains.
   - If the frontend and backend are hosted under different domains, verify SignalR websocket negotiation and gRPC/HTTP fallback behavior through the chosen reverse proxy.
5. **Persist logs, traces, and metrics in a cloud backend**
   
   - Configure OpenTelemetry exporters for Azure Monitor, Application Insights, AWS X-Ray, Amazon CloudWatch, Grafana, or another OTLP-compatible observability stack.
   - Add environment-specific service names and resource attributes so telemetry is easy to filter.
6. **Review health checks and readiness behavior**
   
   - Keep `/health` for container liveness.
   - Add or verify readiness checks for database connectivity if the target platform distinguishes liveness from readiness.
7. **Externalize database migrations strategy**
   
   - Automatic migrations at startup are convenient, but many production teams prefer a release step or init job.
   - Decide whether migrations should run in the backend container, a dedicated migration job, or the CI/CD pipeline.
8. **Containerize and publish all deployable services consistently**
   
   - Build and version container images for `Backend.Api`, `Worker.Ingestion`, and `Frontend.Host`.
   - Push them to Azure Container Registry or Amazon ECR.
   - Treat `AppHost` as local orchestration only, not as the cloud deployment artifact.
9. **Plan for secrets, TLS, and identity**
   
   - Do not store secrets in the repo.
   - Use managed identities in Azure where possible, and IAM roles/task roles in AWS.
   - Terminate TLS at a managed ingress, reverse proxy, or load balancer.
10. **Add CI/CD automation**
    
    - Build, test, publish container images, run migrations, and deploy with GitHub Actions, Azure DevOps, or AWS deployment pipelines.
    - Promote the same images between environments instead of rebuilding them differently for each stage.

### 2. Detailed steps to deploy on Azure

One practical Azure target is **Azure Container Apps + Azure Database for PostgreSQL Flexible Server + Azure Container Registry**.

#### Azure architecture suggestion

- `Backend.Api` → Azure Container Apps service
- `Worker.Ingestion` → Azure Container Apps service or Container Apps job
- `Frontend.Host` → Azure Container Apps service
- PostgreSQL → Azure Database for PostgreSQL Flexible Server
- Secrets/config → Azure Key Vault + Container Apps secrets
- Observability → Azure Monitor / Application Insights

#### Step-by-step Azure deployment

1. **Create an Azure resource group**
   
   - Choose a single region for the app, database, and registry.
   - Example naming: `rg-remotepulse-prod`.
2. **Provision Azure Container Registry (ACR)**
   
   - Create an ACR instance.
   - Enable admin access only if required temporarily; prefer managed identity-based pulls.
3. **Provision Azure Database for PostgreSQL Flexible Server**
   
   - Create the server and database for RemotePulse.
   - Configure firewall or private networking so Azure-hosted services can reach it.
   - Capture the PostgreSQL connection string for the backend.
4. **Build and push container images**
   
   - Build separate images for:
     - `src/Backend.Api`
     - `src/Worker.Ingestion`
     - `src/Frontend.Host`
   - Tag images with an immutable version such as a Git commit SHA.
   - Push all images to ACR.
5. **Create a Container Apps environment**
   
   - Create one managed environment for the workload.
   - Enable Log Analytics / Azure Monitor integration during setup if available.
6. **Deploy the backend container app**
   
   - Configure ingress for HTTP/HTTPS.
   - Set environment variables/secrets such as:
     - `ConnectionStrings__remotepulse`
     - `Cors__AllowedOrigins`
     - telemetry exporter settings
   - Expose the backend publicly if the frontend is hosted separately and needs direct access.
   - Confirm that the backend health endpoint responds successfully.
7. **Deploy the frontend host container app**
   
   - Point `BackendBaseUrl` to the public backend URL.
   - Configure the frontend ingress as external.
   - If using a custom domain, bind TLS certificates through Container Apps or Front Door.
8. **Deploy the ingestion worker**
   
   - Configure worker environment variables so it can resolve the backend URL.
   - If the worker only produces internal traffic, keep it on internal ingress or no ingress.
   - Make sure it targets the backend endpoint that supports gRPC and/or HTTP fallback in your chosen routing setup.
9. **Handle database migrations**
   
   - Either allow the backend to apply migrations at startup or run them through a dedicated release command/job before opening traffic.
   - Validate schema creation before scaling out.
10. **Configure secrets and identity**
    
    - Store secrets in Key Vault or Container Apps secrets.
    - Grant managed identity access for ACR pulls and secret retrieval where supported.
11. **Configure scaling and revisions**
    
    - Set min/max replicas for the backend and frontend.
    - Scale the worker based on queue/event strategy if you later replace the simulator with real ingestion.
    - Use revisions for safe rollouts.
12. **Set up monitoring and alerts**
    
    - Send logs, traces, and metrics to Azure Monitor / Application Insights.
    - Add alerts for failed health checks, high error rates, restart loops, and database saturation.
13. **Validate the deployment**
    
    - Open the frontend URL.
    - Confirm `/health` and `/health/summary` on the backend.
    - Verify that live pulses appear in the dashboard and SignalR updates flow correctly.

#### Azure deployment notes

- If you need edge routing, WAF, or custom domain centralization, place **Azure Front Door** in front of the frontend and backend.
- If gRPC routing through your selected ingress becomes restrictive, keep the worker-to-backend communication on private networking or validate HTTP fallback as the operational backup path.
- For enterprise environments, private endpoints and VNet integration are preferable over broad public access.

### 3. Detailed steps to deploy on AWS

One practical AWS target is **Amazon ECS on Fargate + Amazon RDS for PostgreSQL + Amazon ECR + an Application Load Balancer**.

#### AWS architecture suggestion

- `Backend.Api` → ECS service on Fargate
- `Worker.Ingestion` → ECS service on Fargate
- `Frontend.Host` → ECS service on Fargate
- PostgreSQL → Amazon RDS for PostgreSQL
- Container registry → Amazon ECR
- Secrets/config → AWS Secrets Manager + Systems Manager Parameter Store
- Observability → Amazon CloudWatch + AWS X-Ray / OTEL collectors

#### Step-by-step AWS deployment

1. **Create networking resources**
   
   - Create or choose a VPC with public and private subnets across at least two availability zones.
   - Place the load balancer in public subnets and ECS tasks/RDS in private subnets where possible.
2. **Provision Amazon ECR repositories**
   
   - Create repositories for backend, worker, and frontend images.
   - Use immutable tags or commit SHA tags.
3. **Provision Amazon RDS for PostgreSQL**
   
   - Create a PostgreSQL instance or cluster.
   - Place it in private subnets.
   - Create the application database and store the connection string securely.
4. **Build and push container images**
   
   - Build images for `Backend.Api`, `Worker.Ingestion`, and `Frontend.Host`.
   - Authenticate to ECR and push tagged images.
5. **Create an ECS cluster**
   
   - Use Fargate for simpler operations.
   - Create task execution roles and task roles with only the permissions each service needs.
6. **Create the backend task definition and ECS service**
   
   - Set CPU/memory values.
   - Configure environment variables and secrets such as:
     - `ConnectionStrings__remotepulse`
     - `Cors__AllowedOrigins`
     - OTEL exporter settings
   - Expose the backend through an Application Load Balancer target group.
   - Configure container health checks against `/health`.
7. **Create the frontend task definition and ECS service**
   
   - Set `BackendBaseUrl` to the backend public DNS name or custom domain.
   - Expose the frontend through the load balancer.
   - Optionally route frontend and backend under different paths or hostnames using listener rules.
8. **Create the worker task definition and ECS service**
   
   - Run the worker without public ingress.
   - Point it to the backend internal DNS name, service discovery name, or load balancer endpoint depending on your architecture.
   - Ensure security groups allow outbound worker-to-backend traffic and backend-to-database traffic.
9. **Configure service discovery and traffic**
   
   - Use ECS service discovery / Cloud Map for private service-to-service naming if desired.
   - If the worker uses gRPC, confirm the selected load balancer and protocol settings support the traffic pattern you want.
   - Keep HTTP fallback available if gRPC proxying is not part of the first deployment.
10. **Handle migrations**
    
    - Run EF Core migrations from a one-off ECS task, deployment job, or controlled application startup step.
    - Verify the schema before scaling multiple backend tasks.
11. **Configure secrets and certificates**
    
    - Store secrets in Secrets Manager or Parameter Store.
    - Manage public TLS certificates with AWS Certificate Manager.
    - Terminate TLS at the Application Load Balancer.
12. **Configure logging, tracing, and alarms**
    
    - Send container logs to CloudWatch Logs.
    - Export traces to X-Ray or an OTLP collector.
    - Add CloudWatch alarms for unhealthy targets, task restarts, elevated latency, and database pressure.
13. **Validate the deployment**
    
    - Open the frontend URL.
    - Confirm backend health endpoints.
    - Verify the worker is generating pulse traffic.
    - Confirm the dashboard updates in realtime.

#### AWS deployment notes

- If you want simpler static frontend hosting later, you can host the Blazor WebAssembly assets in **Amazon S3 + CloudFront** and keep only the backend and worker on ECS.
- If you need Kubernetes, the same services can also move to **Amazon EKS**, but ECS/Fargate is usually the simpler first production target for this repository.
- Use private subnets and least-privilege security groups from the start to avoid reworking the network design later.
  
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
