---
title: HAIT Article (2024)
type: entity
status: unreviewed
sources:
  - raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md
  - raw/documents/documents/HAIT_Article_TSP_Ukrainian.md
  - raw/documents/documents/HAIT_Article_Ukrainian_Metadata.md
  - raw/documents/documents/HAIT_Formatting_Instructions.md
  - raw/documents/Article_Critique.md
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
updated: 2026-04-18
---

# HAIT Article — *Parallel GA for TSP in PARCS* (2024)

Journal submission to **Herald of Advanced Information Technology** (HAIT). Describes and evaluates the [TSP Module](tsp-module.md) as a case study for the [PARCS system](parcs-system.md).

## Citation

> Bogusevych, O. V. "Parallel Genetic Algorithm for Traveling Salesman Problem in PARCS Distributed Computing System" // *Herald of Advanced Information Technology.* – 2024. – Vol. X. – No. X. – P. XX–XX.
> UDC: 004.021:004.272.2 · DOI: 10.15276/hait.2024.XX.XX

Author: Oleksandr V. Bogusevych — Department of Computer Science, Faculty of Information Technologies, Odesa. `[source: raw/documents/documents/HAIT_Article_Ukrainian_Metadata.md]`

## Versions in the raw corpus

- **English** — `raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md`
- **Ukrainian mirror** — `raw/documents/documents/HAIT_Article_TSP_Ukrainian.md` (direct translation, same section structure)
- **Extended English draft** — `raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md` (longer variant; source for most of the technical wiki pages)
- **Formatting rules** — `raw/documents/documents/HAIT_Formatting_Instructions.md` (A4, Times New Roman 11pt, single-column metadata + two-column body, 12–14 pages)

## Key claims

- Implements parallel GA on PARCS using a **population-distribution strategy** — each worker evolves a subpopulation with a different random seed.
- **3.78× wall-clock speedup with 4 workers** on a 50-city instance; solution quality maintained or improved.
- Argues PARCS (Kubernetes-based, fault-tolerant) is a good fit for distributed evolutionary computation.

The full benchmark sweep across 500/1000/2000 cities lives in [TSP Benchmarks](tsp-benchmarks.md). Those newer results are *not* in the HAIT draft — the paper's headline is the 50-city experiment.

## Reviewer's critique (unresolved in the drafts)

`[source: raw/documents/Article_Critique.md]` flags several gaps. The paper is sound but under-specified:

1. **Missing implementation details** — no YAML for KEDA `ScaledObject`, no code for daemon lifecycle (read message → connect → work → exit), no error/retry story, no Service Bus message format.
2. **Incomplete related work** — mentions PARCS-NET / PARCS-WCF / PARCS-Kubernetes but no comparison table across them or vs Knative / Fargate+EventBridge.
3. **Missing performance analysis** — no cold-start time, no scale-from-zero latency, no cost comparison vs always-on daemons.
4. **No challenges/limitations section** — cold start, message ordering, poison messages, node-provisioning delay (AKS: 3–5 min).
5. **No security section** — authN between Host and Daemon, secrets for Service Bus, network policies.

These are the most actionable items for a revision pass.

## Related wiki pages

- [PARCS System](parcs-system.md) — the platform the paper describes
- [TSP Module](tsp-module.md) — the case study
- [Genetic Algorithm for TSP](genetic-algorithm.md) — the algorithm the paper parallelises
- [TSP Benchmarks](tsp-benchmarks.md) — newer, broader results than those in the paper
- [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) — context for interpreting the 3.78× speedup claim
