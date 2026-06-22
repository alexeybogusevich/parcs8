---
title: "Source: TSP_Results_Executive_Summary.md"
type: summary
status: unreviewed
sources:
  - raw/documents/TSP_Results_Executive_Summary.md
updated: 2026-04-18
---

# Source summary — TSP Results Executive Summary

Non-technical top-line framing of the benchmark data in `TSP_Results_Analysis.md`.

## Bottom line

**Migration with 3–4 workers** — 20–48% better solutions at 13–25% time overhead.

Best by size:
- 500 cities → Migration (4w), 48% better, +24% time.
- 1000 cities → Migration (4w), 20% better, +24% time.
- 2000 cities → Migration (3w), 26% better, +14% time.

## Recommendations

- **Best quality**: Migration (4w) — 20–48% gain, accept +13–25% time.
- **Best speed**: Island (2w) — 2–19% gain, +0–3% time.
- **Balanced**: Migration (3w) — consistent across sizes.

## Feeds wiki pages

- [TSP Benchmarks](../tsp-benchmarks.md)
- [Parallelization Models](../parallelization-models.md) ("Practical guidance" section)
