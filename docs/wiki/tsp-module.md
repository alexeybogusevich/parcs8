---
title: TSP Module
type: entity
status: unreviewed
sources:
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
  - raw/modules/Parcs.Modules.TravelingSalesman/README.md
  - raw/documents/TSP_Module_Analysis.md
  - raw/documents/TSP_Module_Fixes_Summary.md
updated: 2026-04-18
---

# TSP Module (`Parcs.Modules.TravelingSalesman`)

Reference workload for the PARCS system: a parallel genetic algorithm for the Traveling Salesman Problem. Ships three execution models (Sequential, Parallel island, Master-Slave), acts as the case study in the HAIT paper, and drives the benchmark corpus in [TSP Benchmarks](tsp-benchmarks.md).

## File layout

```
Parcs.Modules.TravelingSalesman/
├── Models/
│   ├── City.cs              # coords + Euclidean distance
│   ├── Route.cs             # chromosome; OX crossover; swap/inversion mutation
│   ├── GeneticAlgorithm.cs  # core evolution loop
│   └── CityLoader.cs        # load/save cities as .txt or .json
├── Sequential/
│   └── SequentialMainModule.cs
├── Parallel/
│   ├── ParallelMainModule.cs
│   └── ParallelWorkerModule.cs
└── ModuleOptions.cs
```

`[source: raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md §5.1]`

## Configuration (`ModuleOptions`)

| Option | Default | Meaning |
|---|---|---|
| `CitiesNumber` | 50 | Cities to generate (when not loaded from file) |
| `PopulationSize` | 1000 | GA population per island |
| `Generations` | 100 | Max generations |
| `MutationRate` | 0.01 | Per-gene mutation probability |
| `CrossoverRate` | 0.8 | Crossover probability |
| `PointsNumber` | 4 | Parallel workers (islands) |
| `Seed` | 42 | Random seed (deterministic runs) |
| `LoadFromFile` | false | Load cities from disk instead of generating |
| `InputFile` | `cities.txt` | Path to .txt or .json |
| `GenerateRandomCities` | true | Fallback if load fails |
| `EnableMigration` | false | Island-model migration (see [Parallelization Models](parallelization-models.md)) |
| `MigrationType` | BestIndividuals | `Best` / `Random` / `Diverse` / `Tournament` |
| `MigrationSize` | 5 | Individuals moved per migration event |
| `MigrationInterval` | 10 | Migrate every N generations |

`[source: raw/modules/Parcs.Modules.TravelingSalesman/README.md]` (Ukrainian), `[source: raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_MIGRATION_README.md]`

## Input formats

- **Text**: `# comments ok` + `ID X Y` lines
- **JSON**: `[{"Id": 0, "X": 0.0, "Y": 0.0}, …]`

Built-in test-data generators produce four patterns: **Random**, **Grid**, **Clustered**, **Circle**. Reference benchmark instances: `eil51`, `eil76`, `eil101` (standard TSPLIB) plus in-repo `tiny_*`, `medium_*`, `large_*` sets.

`[source: raw/documents/modules/Parcs.Modules.TravelingSalesman/Examples/README.md]`

## Known fixes (historical)

Per `[source: raw/documents/TSP_Module_Analysis.md]` → `[source: raw/documents/TSP_Module_Fixes_Summary.md]`:

1. **OX crossover bug** — `currentPos = (currentPos + 1) % size` caused wrap-around that didn't preserve parent2's relative order. Fixed: select a segment from parent1, then fill remaining slots with parent2 cities *in original order*, skipping cities already in the segment.
2. **Population-size division** — each worker was getting `PopulationSize / PointsNumber`, undermining island diversity. Fixed: each island now gets the full `PopulationSize`. Trade-off: higher memory per worker.
3. **Missing init guard** — `GeneticAlgorithm.Evolve()` could be called before `Initialize()`. Fixed with `InvalidOperationException` guard.
4. **Unconnected migration** — earlier migration code ran inside a single worker only, not across workers via PARCS channels. Resolved by the Island+Migration variant.

## Where this runs on PARCS

The Parallel/Island variants use `moduleInfo.CreatePointAsync()` to spin up workers, then `IChannel`s for point-to-point messaging. The Master-Slave variant sends whole route populations over channels each generation — which is where the communication-cost problem appears, see [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md).

## Related wiki pages

- [Genetic Algorithm for TSP](genetic-algorithm.md) — algorithm-level detail
- [Parallelization Models](parallelization-models.md) — Island / Migration / Master-Slave
- [TSP Benchmarks](tsp-benchmarks.md) — measured quality & timing
- [HAIT Article](hait-article.md) — the paper that uses this module as its case study
