---
title: "Source: Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md"
type: summary
status: unreviewed
sources:
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
updated: 2026-04-18
---

# Source summary — Parallel GA for TSP in PARCS (extended draft)

Long-form English draft of the HAIT paper. Longer than the published `HAIT_Article_*` versions and the main technical source for most wiki pages.

## Key claims

- TSP is NP-hard; solution space `(n-1)!/2`; exact methods infeasible >30 cities.
- GA with Order Crossover + swap/inversion mutation + tournament selection + elitism.
- PARCS is structured as Host / Daemon / Portal / communication layer.
- **Parallelization strategy**: population-based — subpopulations per worker, different random seeds, final aggregation picks best.
- Presents this as scalable, diverse, fault-tolerant, load-balanced.

## Module layout documented

```
Parcs.Modules.TravelingSalesman/
├── Models/ (City, Route, GeneticAlgorithm, ModuleOutput)
├── Sequential/ (SequentialMainModule)
├── Parallel/ (ParallelMainModule, ParallelWorkerModule)
└── ModuleOptions.cs
```

## Feeds wiki pages

- [PARCS System](../parcs-system.md)
- [TSP Module](../tsp-module.md)
- [Genetic Algorithm for TSP](../genetic-algorithm.md)
- [Parallelization Models](../parallelization-models.md)
- [HAIT Article](../hait-article.md)
