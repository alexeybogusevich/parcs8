---
title: "Source: TSP_Results_Analysis.md"
type: summary
status: unreviewed
sources:
  - raw/documents/TSP_Results_Analysis.md
updated: 2026-04-18
---

# Source summary — TSP Results Analysis

Primary benchmark data. Tables for 500 / 1000 / 2000-city TSP runs across Sequential, Island (2/3/4 workers), Island+Migration (2/3/4 workers).

Raw data transcribed into [TSP Benchmarks](../tsp-benchmarks.md).

## Positive trends (marked "✅ real")

- Migration > no-migration at every size.
- More workers → better results (with one exception, see below).
- Time overhead scales reasonably (1–25%).
- Larger problems show larger *absolute* improvement.

## ⚠️ Anomalies the author flagged

1. **500-city Migration gives 43–48% improvement** — suspiciously large for an ensemble. Likely baseline-config issue.
2. **1000-city Island (3w) worse than (2w)** — non-monotone; likely seed variance.
3. **2000-city sequential is 369 s** — disproportionately slow relative to 1000 cities (15 s). 24× for 2× the cities suggests sequential ran with different (larger) parameters.
4. **Improvement percentages don't scale linearly with problem size** — expected for % but the inconsistency suggests config drift between runs.

These should be re-run with locked seeds/params before external citation.

## Feeds wiki pages

- [TSP Benchmarks](../tsp-benchmarks.md) (primary)
- [Parallelization Models](../parallelization-models.md) (quality ranking)
