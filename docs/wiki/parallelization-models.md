---
title: Parallelization Models
type: concept
status: unreviewed
sources:
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
  - raw/documents/TSP_Results_Analysis.md
  - raw/documents/TSP_Results_Executive_Summary.md
  - raw/documents/MasterSlave_Performance_Analysis.md
  - raw/documents/MasterSlave_Optimizations.md
  - raw/documents/Distributed_Computing_Demonstration_Strategy.md
  - raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_BENEFITS.md
  - raw/modules/Parcs.Modules.TravelingSalesman/MASTER_SLAVE_README.md
  - raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_MIGRATION_README.md
updated: 2026-04-18
---

# Parallelization Models for Distributed GA

Three models ship in the [TSP Module](tsp-module.md) — **Sequential**, **Island**, **Island + Migration**, **Master-Slave**. They have different communication patterns and different answers to "does parallel actually help?"

## Summary

| Model | Wall-clock | Solution quality | Communication | When it wins |
|---|---|---|---|---|
| Sequential | Baseline | Good | None | Small problems, tight budgets |
| **Island** | Same as sequential | **Better** (ensemble effect) | None | Medium-large problems; "quality over speed" demos |
| **Island + Migration** | +13–25% overhead | **Best** (20–48% gain) | Periodic | Almost all TSP sizes — strong default |
| Master-Slave | Faster only when comms < fitness-eval saved | Same | High (every generation) | Very large problems: ≥5000 cities + ≥2000 population |

`[source: raw/documents/TSP_Results_Executive_Summary.md]`, `[source: raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_BENEFITS.md]`

## Island Model

Each worker runs an **independent full GA** on its own subpopulation (actually: same population size, different seed). No communication during evolution. At the end, the Host picks the best route across workers.

- Pro: zero comms, embarrassingly parallel, consistently better quality from the ensemble effect.
- Con: **does not speed up wall-clock time** — same time as sequential, just better answer. `[source: raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_BENEFITS.md]`
- Historical gotcha: early versions divided `PopulationSize` by worker count, which wrecked island diversity. See [TSP Module](tsp-module.md) for the fix.

## Island + Migration

Island model with periodic exchange of individuals between workers in a ring topology. Configured via `MigrationInterval`, `MigrationSize`, `MigrationType` (Best / Random / Diverse / Tournament).

- **Consistently the quality winner** across all benchmarked sizes — see [TSP Benchmarks](tsp-benchmarks.md).
- Cost: 13–25% time overhead vs plain Island for the migration phases.
- `BestIndividuals` spreads winners fast but can collapse diversity; `Diverse` / `Tournament` are the safer defaults. `[source: raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_MIGRATION_README.md]`

## Master-Slave (Farming)

Master keeps a single population. Each generation: selection/crossover/mutation on master, workers get batches of routes to evaluate fitness in parallel, results flow back.

**In practice it's slower than sequential for the problem sizes we actually run.** Measured at 2000 cities: 566 s master-slave vs 177 s sequential — **3.2× slower** because serialization + network transfer (~400 s) dwarfs the fitness-eval savings (~11 s). `[source: raw/documents/MasterSlave_Performance_Analysis.md]`

Break-even requires all of:
- ≥5000 cities (fitness-eval large enough to matter),
- ≥2000 population (amortise per-generation comms),
- low-latency channels, and
- **binary (not JSON) serialization**.

The `MasterSlave_Optimizations.md` doc describes switching to `BinaryWriter`/`BinaryReader` + list pre-sizing, which cuts ~70% of communication time (~400 s → ~120 s at 2000 cities) but *still* doesn't beat sequential at that size. At 10 000 cities + 2000 population, the optimised master-slave hits ~2.3× speedup over sequential. `[source: raw/documents/MasterSlave_Optimizations.md]`

## Practical guidance

- **Default**: Island + Migration with 3–4 workers — best quality/overhead across all sizes.
- **Best quality, cost irrelevant**: Migration with 4 workers.
- **Best speed, lowest overhead**: Island (no migration), 2 workers — small quality win, 0–3% overhead.
- **To actually demonstrate speedup** (as opposed to quality), pick an embarrassingly parallel workload — ProofOfWork or Monte Carlo — not TSP. `[source: raw/documents/Distributed_Computing_Demonstration_Strategy.md]`

See also [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) for the speedup-vs-efficiency framing and why "4 workers, 94.5% efficiency" isn't the same as "4× less total compute".
