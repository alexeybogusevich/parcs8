---
title: "Source: TSP_Parallel_Speed_vs_Efficiency.md"
type: summary
status: unreviewed
sources:
  - raw/documents/TSP_Parallel_Speed_vs_Efficiency.md
updated: 2026-04-18
---

# Source summary — Parallel: Speed vs Efficiency

Clarification piece disambiguating two claims that often get conflated: "parallel is faster" and "parallel uses less compute".

## Two metrics, two answers

**Wall-clock time (user-perceived speed)** — parallel IS faster.
- 50 cities: sequential 45.7 s, parallel 12.1 s on 4 workers → 3.78× speedup.

**Total computational work (CPU-seconds burned)** — parallel uses MORE.
- 4 × 12.1 s = 48.4 CPU-seconds vs 45.7 CPU-seconds sequential.
- 94.5% efficiency means "good parallelisation", not "less compute".

## Framework

- **Speedup** = T_seq / T_par
- **Efficiency** = Speedup / N_workers
- **Total work** = N_workers × T_par

In cloud per-CPU-hour pricing, parallel is slightly more expensive for the same job. Value proposition: you pay a few % extra to finish sooner and (for ensemble methods) get a better answer.

## When each makes sense

**Use parallel when**:
- Multiple cores available.
- Time matters more than cost.
- Ensemble/quality gains are valuable.
- Resources are abundant (modern clusters).

**Prefer sequential when**:
- Limited cores / battery-powered.
- Per-CPU-hour cost dominates.
- Sequential quality is sufficient.

## Feeds wiki pages

- [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md) (primary — the "speedup ≠ efficiency" section)
