# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

PARCS-NET-K8 — a .NET 10 platform for running recursive parallel computation "modules" across a cluster of daemon workers. Deploys to AKS (Azure), GKE (GCP) or locally via Docker Compose / a single-node Kubernetes manifest. Depends on the external NuGet package `Parcs.Net` (currently 10.0.0) which defines the module-author SDK (`IModule`, `IPoint`, `IChannel`, …).

## Build, run, test

The solution is `Parcs7.sln` at the repo root. All projects target `net10.0`.

```bash
# Restore + build everything
dotnet build Parcs7.sln

# Build a single project
dotnet build src/Parcs.Host/Parcs.Host.csproj

# Run all tests (NUnit)
dotnet test Parcs7.sln

# Run one test project / one test
dotnet test tests/Parcs.Host.Tests/Parcs.Host.Tests.csproj
dotnet test --filter "FullyQualifiedName~SomeTestName"

# Local stack (Daemon + Host API + Portal + Postgres + Elasticsearch + Kibana)
# NOTE: compose file lives under src/, not the repo root.
docker-compose -f src/docker-compose.yml up --build

# Single-node Kubernetes dev deployment
kubectl apply -f kube/deployment.local.yaml
# Cloud deployments
kubectl apply -f kube/deployment.azure.yaml
kubectl apply -f kube/deployment.gcp.yaml
```

EF Core migrations are created against `Parcs.Data` but **applied automatically at Host startup** via `app.MigrateDatabase()` in [src/Parcs.Host/Program.cs](src/Parcs.Host/Program.cs) — do not run `dotnet ef database update` manually. To add a migration:

```bash
dotnet ef migrations add <Name> --project src/Parcs.Data --startup-project src/Parcs.Host
```

Logs from `parcs-daemon` and `parcs-hostapi` go to Elasticsearch via Serilog. In Kibana, create a data view on index pattern `parcs-*` to browse them.

## Architecture

The system is a set of cooperating services built around a **module execution protocol**. A user submits a compiled "module" (a DLL implementing `Parcs.Net.IModule`); the Host API schedules it onto one or more Daemon workers, which dynamically load the assembly and run it in an isolated `AssemblyLoadContext`, communicating with other workers over TCP channels.

### Services (`src/`)

- **Parcs.Host** — ASP.NET Core Web API. The control plane. Accepts job/module CRUD + run requests, stores state in Postgres via `Parcs.Data`, orchestrates daemons. Uses **MediatR CQRS** — each endpoint in `Controllers/` delegates to a handler in `Handlers/` (`*CommandHandler`, `*QueryHandler`). Two run models: `SynchronousJobRunsController` (blocks until done — Kestrel timeouts are bumped to 15 min in `Program.cs` for this) and `AsynchronousJobRunsController`. Also runs its own `HostTcpServer` that daemons connect back to. Schedules new daemon "points" via the Kubernetes client or (on GCP) Pub/Sub + KEDA scaling (see `PointCreationService`).
- **Parcs.Daemon** — headless worker process. Runs three hosted services (see [src/Parcs.Daemon/Program.cs](src/Parcs.Daemon/Program.cs)): `InternalServer` (intra-daemon channels), `TcpServer` (port 1111, receives signals from Host/other daemons), `PointCreationConsumer` (GCP Pub/Sub consumer for the KEDA-driven scale-out path). Dispatches incoming signals to handlers in `Handlers/` via `SignalHandlerFactory` (initialize job, execute class, cancel job, …). Module assemblies are loaded into isolated contexts provided by `Parcs.Core`.
- **Parcs.Portal** — Blazor Server UI for managing modules/jobs. Pages in `Pages/` (`Modules.razor`, `Jobs.razor`, `NewJob.razor`, `RunJob.razor`, …). Talks to the Host API over HTTP and uses a SignalR hub for live job updates.
- **Parcs.Agent.Mcp** — Model Context Protocol server that exposes PARCS as tools to AI agents. Sessions compile a user-supplied `IAgentComputation` C# class via Roslyn; each `run_layer` call fans out that code across N daemons, threading `previousLayerResultJson` from one layer to the next. See [src/Parcs.Agent.Mcp/Tools/ParcsAgentTools.cs](src/Parcs.Agent.Mcp/Tools/ParcsAgentTools.cs) for the contract. The `Parcs.Modules.AgentRunner` module is the daemon-side counterpart that actually executes the compiled code.

### Shared libraries (`src/`)

- **Parcs.Net** — thin interface-only package (`IModule`, `IPoint`, `IChannel`, `IInputReader`, `IOutputWriter`, …) shared with module authors. Pulled from NuGet, **not** a project reference; changes to these interfaces mean bumping the `Parcs.Net` package version in `Parcs.Core.csproj`.
- **Parcs.Core** — shared implementation used by both Host and Daemon: daemon resolution strategies (`ConfigurationDaemonResolutionStrategy` vs `KubernetesDaemonResolutionStrategy`), module loading (`ModuleLoader`, `IsolatedLoadContext`, `TypeLoader`), channel management, path builders.
- **Parcs.Data** — EF Core DbContext, entities (`JobEntity`, `JobStatusEntity`, `JobFailureEntity`, `ModuleEntity`), and migrations. Postgres only.
- **Parcs.Agent.Runtime** — base types (`IAgentComputation`, `AgentLayerInput`, `AgentLayerResult`) that the MCP server compiles user code against.

### Modules (`modules/`)

Sample / benchmark implementations of `IModule`: `Sample`, `Integral`, `MatrixesMultiplication`, `MonteCarloPi`, `FloydWarshall`, `ProofOfWork`, `TravelingSalesman`, and `AgentRunner` (the counterpart for the MCP agent path). Each is a separate assembly loaded at runtime by the daemon — they are **not** referenced by Host/Daemon projects. `Parcs.Modules.Sample/MainModule.cs` is the canonical reference for the module-author API.

### Infra / deployment

- `kube/deployment.{local,azure,gcp}.yaml` — one flat manifest per target; deploys daemon, hostapi, portal, postgres, elasticsearch, kibana.
- `infra/` — `main.bicep` / `resources.bicep` for Azure; `infra/gcp/main.tf` for GCP (Terraform).
- Docker images built from per-project `Dockerfile`s (`src/Parcs.Daemon/Dockerfile`, etc.) with the **build context set to the repo `src/` root** (see `DockerfileContext` in each `.csproj`) so they can reach sibling projects.

## Conventions worth knowing

- **No `dotnet ef database update`** — the Host self-migrates on boot; adding a migration and rebuilding the Host image is the full workflow.
- **Never add a `ProjectReference` from a module to Host/Daemon/Core.** Modules only depend on `Parcs.Net` (the NuGet). Module assemblies are discovered and loaded at runtime — referencing the host process breaks the isolation boundary.
- **Synchronous jobs depend on bumped Kestrel timeouts.** If you add/modify long-running endpoints, check `builder.WebHost.ConfigureKestrel(...)` in the Host's `Program.cs` first.
- **Logging is Serilog → Elasticsearch**, wired via `AddElasticsearchLogging` / `AddElasticSearchLogging` extensions. Application Insights has been removed — don't reintroduce it.
- Host-side work uses **MediatR**; add new operations as a `*Command`/`*Query` + handler pair under `Handlers/`, then expose via a controller, rather than putting logic directly in controllers.
