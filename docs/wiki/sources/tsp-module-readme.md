---
title: "Source: modules/Parcs.Modules.TravelingSalesman/README.md"
type: summary
status: unreviewed
sources:
  - raw/modules/Parcs.Modules.TravelingSalesman/README.md
updated: 2026-04-18
---

# Source summary — TSP Module README (Ukrainian)

Module-adjacent documentation for `Parcs.Modules.TravelingSalesman`. Written in Ukrainian but covers concrete config and usage.

## What it documents

- Module components: `City`, `Route`, `GeneticAlgorithm`, `CityLoader`, `ModuleOptions`, `ModuleOutput`.
- Three execution variants: `SequentialMainModule`, `ParallelMainModule`, `ParallelWorkerModule`.
- **File input support**: `.txt` (`ID X Y` per line, `#` comments) and `.json` (`[{"Id":…,"X":…,"Y":…}, …]`).
- **Generator patterns** for test data: Random, Grid, Clustered, Circle.
- **Deterministic runs** via fixed seed.

## Configuration surface (copied verbatim into [TSP Module](../tsp-module.md))

`ModuleOptions` fields include `CitiesNumber`, `PopulationSize`, `Generations`, `MutationRate`, `CrossoverRate`, `PointsNumber`, `Seed`, plus the file-load triplet `LoadFromFile` / `InputFile` / `GenerateRandomCities`.

## Config presets shown in README

- Small (25 cities, pop 500, gen 50) — `small_grid_16.txt`
- Medium (64 cities, pop 1000, gen 200) — `medium_clustered_60.json`
- Large (144 cities, pop 2000, gen 500, 8 points) — file-driven

## Feeds wiki pages

- [TSP Module](../tsp-module.md) (primary — config table)
