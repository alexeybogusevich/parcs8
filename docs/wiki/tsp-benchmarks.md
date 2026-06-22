---
title: TSP Benchmarks
type: summary
status: unreviewed
sources:
  - raw/documents/TSP_Results_Analysis.md
  - raw/documents/TSP_Results_Executive_Summary.md
  - raw/documents/MasterSlave_Performance_Analysis.md
updated: 2026-04-18
---

# TSP Benchmarks

Measured results for the [TSP Module](tsp-module.md) across problem sizes and [parallelization models](parallelization-models.md). "Improvement" is solution-quality improvement (shorter tour), not time speedup.

## 500 cities

| Config | Best distance | Time (s) | Quality Δ | Time Δ |
|---|---|---|---|---|
| Sequential | 52 867.97 | 9.66 | baseline | baseline |
| Island (2w) | 42 790.95 | 9.52 | −18.9% | −1.5% |
| Island (3w) | 39 101.23 | 10.17 | −26.0% | +5.3% |
| Island (4w) | 38 762.38 | 10.44 | −26.7% | +8.1% |
| Island+Mig (2w) | 30 000.53 | 10.95 | −43.3% | +13.4% |
| Island+Mig (3w) | 29 758.11 | 11.89 | −43.7% | +23.1% |
| **Island+Mig (4w)** | **27 341.94** | 12.03 | **−48.3%** | +24.5% |

## 1000 cities

| Config | Best distance | Time (s) | Quality Δ | Time Δ |
|---|---|---|---|---|
| Sequential | 115 804.61 | 15.41 | baseline | baseline |
| Island (2w) | 113 429.18 | 15.92 | −2.0% | +3.3% |
| Island (3w) | 114 110.21 | 16.27 | **−1.5%** ⚠️ worse than 2w | +5.6% |
| Island (4w) | 107 288.94 | 16.01 | −7.4% | +3.9% |
| Island+Mig (2w) | 101 187.02 | 18.32 | −12.6% | +18.9% |
| Island+Mig (3w) | 98 386.98 | 18.76 | −15.0% | +21.7% |
| **Island+Mig (4w)** | **92 827.55** | 19.03 | **−19.8%** | +23.5% |

## 2000 cities

| Config | Best distance | Time (s) | Quality Δ | Time Δ |
|---|---|---|---|---|
| Sequential | 168 619.58 | 369.08 | baseline | baseline |
| Island (2w) | 161 218.37 | 381.21 | −4.4% | +3.3% |
| Island (3w) | 144 992.22 | 394.43 | −14.0% | +6.9% |
| Island (4w) | 141 794.90 | 397.25 | −15.9% | +7.6% |
| Island+Mig (2w) | 128 335.76 | 415.37 | −23.9% | +12.5% |
| **Island+Mig (3w)** | **125 449.26** | 421.38 | **−25.6%** | +14.2% |
| Island+Mig (4w) | 125 979.33 | 417.96 | −25.3% | +13.2% |

## Headlines

- **Migration always beats no-migration.** 13–48% quality gain at 13–25% time overhead.
- **3–4 workers is the sweet spot.** 4 workers wins at 500 and 1000 cities; 3 workers wins at 2000 (less coordination overhead).
- **Island-only (no migration) gives 2–27% quality gain at near-zero time cost** (0–8% overhead).
- **Master-Slave** does *not* appear in these tables for a reason — it's 3.2× slower than sequential at 2000 cities, see [Parallelization Models](parallelization-models.md).

## Anomalies flagged in the raw analysis

The raw `TSP_Results_Analysis.md` explicitly calls these out as suspicious — they should be re-run before quoting externally:

1. **500-city migration gain (43–48%) is abnormally large** for an ensemble effect; may indicate a baseline bug (sequential running with different params? different seed across runs?).
2. **1000-city Island (3w) is worse than Island (2w)** (114 110 vs 113 429). Should be monotone in a well-mixed ensemble — likely seed variance.
3. **2000-city sequential (369 s) is disproportionately slow** vs 1000-city sequential (15 s). Scaling should be ~4× not 24×. Possibly larger population or generations in the sequential run.

`[source: raw/documents/TSP_Results_Analysis.md §⚠️ Suspicious Findings]`

## HAIT paper's published number

The HAIT submission headlines a **3.78× wall-clock speedup on 50 cities with 4 workers**. That is a different experiment from the tables above (smaller, and measuring time not quality). Not directly comparable — see [HAIT Article](hait-article.md).

## Related wiki pages

- [Parallelization Models](parallelization-models.md) — what each config is
- [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) — why "faster" vs "better" is a different question
- [TSP Module](tsp-module.md) — module under test
