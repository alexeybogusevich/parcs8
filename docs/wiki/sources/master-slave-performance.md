---
title: "Source: MasterSlave_Performance_Analysis.md"
type: summary
status: unreviewed
sources:
  - raw/documents/MasterSlave_Performance_Analysis.md
updated: 2026-04-18
---

# Source summary — Master-Slave Performance Analysis

Diagnostic write-up for why Master-Slave was measured **3.2× slower than sequential** at 2000 cities (566 s vs 177 s).

## Root cause

Per-generation communication dominates. Estimated for 2000 cities / 1000 routes / 100 generations with JSON serialization:

| Stage | Time |
|---|---|
| Selection/Crossover/Mutation | ~10 s |
| JSON serialization | ~100 s |
| Network transfer | ~200 s |
| Deserialization | ~100 s |
| Parallel fitness evaluation | ~156 s (vs ~167 s sequential — only 11 s saved) |
| **Total** | **~566 s** |

Communication overhead (~400 s) >> parallel speedup benefit (~11 s). Total data/generation ≈ 16–24 MB after JSON overhead → ~1.6–2.4 GB transferred per 100-generation run.

## Conditions under which Master-Slave works

- Large problem size (**≥10 000 cities**).
- Large population (≥5000 routes).
- Many generations to amortise startup.
- Fast network.
- **Binary** serialization (not JSON).

## Proposed solutions

1. Hybrid: use Master-Slave only for ≥5000 cities + ≥2000 population.
2. Optimise comms: binary serialization, batching, compression, delta encoding.
3. Reduce comms frequency: workers evolve independently for N generations, sync periodically.
4. Switch strategy entirely: Island Model has zero comms and better quality.

## Feeds wiki pages

- [Parallelization Models](../parallelization-models.md) (Master-Slave section)
- [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md) (primary — break-even conditions)
