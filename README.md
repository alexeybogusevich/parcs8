# PARCS-NET-K8

## Overview

PARCS-NET-K8 is a .NET 10 platform for running recursive parallel computations on Kubernetes. A user
uploads a **module**, a compiled DLL that implements `Parcs.Net.IModule`. The Host API schedules it
onto daemon workers. Each worker loads the assembly into an isolated `AssemblyLoadContext` and runs it,
talking to the other workers over TCP channels.

The platform runs on Azure (AKS), Google Cloud (GKE), AWS (EKS), a single-node Kubernetes cluster or
Docker Compose. AI agents can drive it through an MCP server.

## Architecture

| Service | Role |
|---|---|
| `Parcs.Host` | ASP.NET Core Web API and control plane: modules, jobs, synchronous and asynchronous runs. Stores state in Postgres and requests points from daemons. |
| `Parcs.Daemon` | Headless worker. Loads modules and runs points. Points can create nested points. |
| `Parcs.Portal` | Blazor Server UI for managing modules and jobs. |
| `Parcs.Agent.Mcp` | Model Context Protocol server that exposes PARCS to AI agents (see [AI agents](#ai-agents)). |
| `Parcs.Core`, `Parcs.Data`, `Parcs.Agent.Runtime` | Shared implementation, EF Core data layer (migrations run at Host startup) and the base types for agent-written computations. |

Points are scheduled in one of two ways. In **Elastic** mode, points are requested through a queue
(Service Bus, Pub/Sub or SQS) and KEDA scales daemon Jobs on it. In **Static** mode, points go to a
fixed pool of daemons. See [docs/scaling-modes.md](docs/scaling-modes.md).

## Writing modules

Modules depend only on the [`Parcs.Net`](https://www.nuget.org/packages/Parcs.Net/) NuGet package
(version **10.1.0**), never on Host, Daemon or Core. The module-author API supports more than a star
around the main point:

- **Peer channels**: `input.Peer(j)` opens a channel to a sibling point on demand. This makes rings,
  hypercubes and all-reduce possible without the main point relaying messages.
- **Point links**: `LinkPointsAsync` / `GetLinkAsync` connect two points directly.
- **Spawning**: `SpawnAsync` lets a point create its own children, so trees can grow from inside one job.
- **Non-blocking receive**: `HasMessage`, `TryReceive` and `ReceiveAnyAsync`.

Sample modules live in [`modules/`](modules). [`Parcs.Modules.Sample`](modules/Parcs.Modules.Sample/MainModule.cs)
is the reference for the API. The topology examples are `Ring`, `Tree` and `Hypercube`. The
computational benchmarks are `Integral`, `MonteCarloPi`, `MatrixesMultiplication`, `FloydWarshall`,
`NBody`, `ProofOfWork` and `TravelingSalesman`.

## Build and test

```bash
dotnet build Parcs7.sln
dotnet test Parcs7.sln
```

## Deployment

| Target | How |
|---|---|
| Docker Compose | `docker-compose -f src/docker-compose.yml up --build` |
| Single-node Kubernetes | `kubectl apply -f kube/deployment.local.yaml` |
| Azure AKS | `kube/deployment.azure.yaml`; infrastructure in `infra/main.bicep` |
| Google GKE | [docs/gcp-deployment-guide.md](docs/gcp-deployment-guide.md), `kube/deployment.gcp.yaml` |
| AWS EKS | [docs/aws-deployment-guide.md](docs/aws-deployment-guide.md), `kube/aws-karpenter.yaml` then `envsubst < kube/deployment.aws.yaml \| kubectl apply -f -`; `kube/aws-pause.sh` and `kube/aws-resume.sh` scale an idle cluster to zero and back |

Docker images are built from each project's `Dockerfile` with the repository root as the build
context. Size CPU and memory limits and the number of daemons to the nodes you choose.

## AI agents

`Parcs.Agent.Mcp` lets an agent compile C# `IAgentComputation` classes in a session and run them as
**layers** across N daemons. Each layer can read the results of earlier layers, and the topology
features above are available inside a layer. The tools are built for programmatic tool calling: every
call is bounded by `waitSeconds`, `layerKey` makes layer calls idempotent, and every tool returns one
`{ok, status, …, error}` envelope.

Python packages under [`misc/`](misc):

| Package | Purpose |
|---|---|
| [`parcs_agent`](misc/parcs_agent) | LangChain middleware that connects an agent to the MCP server in `tool_calling` or `ptc` mode, with guidance prompts. |
| [`parcs_agent_cli`](misc/parcs_agent_cli) | Runs one task against a live cluster, traced in LangSmith. |
| [`eval_utils`](misc/eval_utils) | Settings and the model factory shared by the harnesses. |
| [`tool_calling_vs_ptc`](misc/tool_calling_vs_ptc) | Benchmark of tool calling against programmatic tool calling (PTC) across five task families and plan sizes N = 2 to 32. |

The benchmark's main result is that **who orchestrates decides the gap**. When the agent starts every
layer itself (fan-out, a tree of layers or a chain of layers), PTC's advantage in model calls, tokens
and cost grows with N, up to about 11× in cost at N = 32. When the cluster carries the topology inside
one layer (spawned trees, peer all-reduce), tool calling needs only about four model calls at any N,
and PTC's extra turn to write the script makes the two arms roughly even. The method is in
[misc/tool_calling_vs_ptc/README.md](misc/tool_calling_vs_ptc/README.md), and the full results of the
534 sweep runs are in the [PARCS Control-Space Sweeps](https://claude.ai/artifact/BSvDtPjDYjTf6psJPL5Afd)
report.

## Logs in Kibana

Logs from `parcs-daemon` and `parcs-hostapi` go to Elasticsearch through Serilog. To browse them in
Kibana (port 5601):

1. Open **Stack Management** > **Data Views** > **Create data view**.
2. Enter the index pattern `parcs-*` and save it.
3. Use **Discover** to filter by service, log level and time.

## Contributing

Issues and pull requests are welcome.

## License

[MIT](LICENSE)
