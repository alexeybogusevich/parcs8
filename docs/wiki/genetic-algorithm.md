---
title: Genetic Algorithm for TSP
type: concept
status: unreviewed
sources:
  - raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md
  - raw/documents/TSP_Detailed_Explanation.md
  - raw/documents/TSP_Complete_Example.md
  - raw/documents/TSP_Module_Analysis.md
  - raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md
updated: 2026-04-18
---

# Genetic Algorithm for TSP

The evolutionary core shared by all three execution models in the [TSP Module](tsp-module.md).

## Representation

**Chromosome = permutation of city IDs** (e.g. `[0, 3, 1, 4, 2]`), ensuring each city appears exactly once. Fitness is the inverse of total Euclidean tour distance. `[source: raw/documents/TSP_Detailed_Explanation.md]`

```
Total Distance = Σ w(π(i), π((i mod n) + 1))
fitness(route) = 1 / Total Distance
```

## Operators

| Stage | Operator | Notes |
|---|---|---|
| Selection | **Tournament** | Pick best of 3 random individuals; biased toward fit routes |
| Crossover | **Order Crossover (OX)** | Copy a contiguous segment from parent1; fill remaining slots with parent2's cities in original order, skipping duplicates. Historical bug: see [TSP Module](tsp-module.md). |
| Mutation | **Swap** (primary), **Inversion** (alt) | Swap two cities, or reverse a random segment |
| Elitism | Keep the best individual each generation | Prevents regression |

## Main loop

```
Initialize()                  // random population
for g in 0..maxGenerations:
    Evolve()                  // select → crossover → mutation → elitism
    TrackConvergence(best_distance)
    if noImprovement(10):     // early stopping
        break
```

Default parameters (from `ModuleOptions`): population 1000, generations 100, crossover 0.8, mutation 0.01, tournament size 3, seed 42.

## Complexity & why GA

- Solution space: `(n-1)!/2` — for 50 cities that's ~3×10⁶², branch-and-bound is infeasible beyond ~20–30 cities. `[source: raw/documents/TSP_Detailed_Explanation.md]`
- GA gives near-optimal answers in polynomial time per generation, parallelises well (each island = independent GA), and needs no TSP-specific heuristic beyond distance.

## Walkthrough

A hand-run 5-city trace with population 6, 5 generations lives at `[source: raw/documents/TSP_Complete_Example.md]` — useful for sanity-checking operator behaviour.

## Parallel variants

See [Parallelization Models](parallelization-models.md) for how the single-island GA above composes into Island, Island+Migration, and Master-Slave on PARCS.

## Related wiki pages

- [TSP Module](tsp-module.md) — config surface + file layout
- [Parallelization Models](parallelization-models.md) — composition into distributed variants
- [TSP Benchmarks](tsp-benchmarks.md) — what the GA actually converges to at 500/1000/2000 cities
