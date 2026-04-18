---
title: PARCS System Overview
type: entity
status: unreviewed
sources:
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
  - raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md
  - raw/documents/Article_Critique.md
updated: 2026-04-18
---

# PARCS (Parallel Computing System)

A .NET 8 distributed computing framework for running parallel computation "modules" across a cluster of daemon workers, deployed on Kubernetes (AKS / GKE / local). The wiki's corpus treats PARCS as both a platform and the subject of the case study in the HAIT paper.

## Components

- **PARCS Host** — central coordinator. Accepts module/job submissions, distributes work, collects results. ASP.NET Core Web API. See [CLAUDE.md](../../CLAUDE.md) for the in-repo code pointers.
- **PARCS Daemon** — worker nodes that execute computational tasks. TCP server on port 1111; loads module assemblies into isolated contexts.
- **PARCS Portal** — Blazor Server web UI for module management, job submission, monitoring.
- **Communication Layer** — point-to-point channels between daemons + Host↔Daemon TCP; point-creation messaging migrated from Azure Service Bus to GCP Pub/Sub.

`[source: raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md §4.1]`

## Execution model

A **job** runs a **module** (DLL implementing `IModule`) across N **points** (daemon-owned execution contexts). The module's main routine creates points dynamically, opens `IChannel`s to them, ships data, collects results. This is the primitive used by the TSP module's Island and Master-Slave variants — see [Parallelization Models](parallelization-models.md).

## Deployment & autoscaling

- Kubernetes manifests: `kube/deployment.{local,azure,gcp}.yaml` each bundle daemon, hostapi, portal, postgres, elasticsearch, kibana.
- **KEDA + message queue** is the scale-out path: Host publishes a point-request message, KEDA scales daemon pods from 0→N, workers consume the message, connect back to Host, process, exit. Azure Service Bus was the original transport; GCP Pub/Sub is the current one `[source: raw/documents/Article_Critique.md §3]`.
- The HAIT paper reports **3.78× wall-clock speedup with 4 workers** on a 50-city TSP instance `[source: raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md abstract]`. But see [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) — that number doesn't generalise to all workloads or sizes.

## Observed gaps (from the critique)

Reviewer flagged that the published PARCS writeup under-specifies: KEDA `ScaledObject` YAML, daemon lifecycle (read-message → connect → work → exit), error/retry handling, cost comparison vs always-on daemons, cold-start latency for new pods (AKS Cluster Autoscaler can take 3–5 min), Service Bus poison-message handling, and security (authN between Host and Daemon, Service Bus secrets). `[source: raw/documents/Article_Critique.md]`

## Related wiki pages

- [TSP Module](tsp-module.md) — the canonical compute workload on PARCS
- [Parallelization Models](parallelization-models.md) — Island / Migration / Master-Slave on PARCS channels
- [HAIT Article](hait-article.md) — the 2024 journal submission describing PARCS+TSP
- [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) — where PARCS speedup claims hold and break
