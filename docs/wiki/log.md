# Wiki Log

Append-only chronological record of ingest / query / lint operations. Format:
`## [YYYY-MM-DD] <op> | <source> — <takeaway> → updated: …; new: …`

---

## [2026-04-18] bootstrap | Wiki created from 35 raw sources staged in docs/raw/; index.md + log.md written
## [2026-04-18] ingest | Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md — structural paper on parallel GA + PARCS population-based strategy → new: parcs-system.md, genetic-algorithm.md, parallelization-models.md, tsp-module.md, sources/parallel-ga-paper.md
## [2026-04-18] ingest | HAIT_Article_TSP_Parallel_Genetic_Algorithm.md — HAIT English journal submission, 3.78× speedup headline claim → updated: parcs-system.md, genetic-algorithm.md, parallelization-models.md; new: hait-article.md, sources/hait-article-english.md
## [2026-04-18] ingest | HAIT_Article_TSP_Ukrainian.md — Ukrainian mirror of the HAIT article → updated: hait-article.md; new: sources/hait-article-ukrainian.md
## [2026-04-18] ingest | Article_Critique.md — reviewer notes: missing impl details, no cost analysis, no security discussion → updated: hait-article.md; new: sources/article-critique.md
## [2026-04-18] ingest | TSP_Results_Analysis.md — benchmark tables for 500/1000/2000 cities + flagged anomalies → new: tsp-benchmarks.md, sources/tsp-results-analysis.md
## [2026-04-18] ingest | TSP_Results_Executive_Summary.md — top-line conclusion: Migration+3-4 workers wins → updated: tsp-benchmarks.md, parallelization-models.md; new: sources/tsp-results-executive.md
## [2026-04-18] ingest | MasterSlave_Performance_Analysis.md — Master-Slave is 3.2× SLOWER at 2000 cities, comms dominates → updated: parallelization-models.md; new: distributed-computing-tradeoffs.md, sources/master-slave-performance.md
## [2026-04-18] ingest | MasterSlave_Optimizations.md — binary serialization cuts overhead ~70% but still needs ≥5000 cities to beat sequential → updated: parallelization-models.md, distributed-computing-tradeoffs.md; new: sources/master-slave-optimizations.md
## [2026-04-18] ingest | TSP_Parallel_Speed_vs_Efficiency.md — speedup 3.78× ≠ efficiency; parallel uses MORE total CPU-seconds → updated: distributed-computing-tradeoffs.md; new: sources/speed-vs-efficiency.md
## [2026-04-18] ingest | Distributed_Computing_Demonstration_Strategy.md — recommendation: demo with Island Model (quality) + ProofOfWork/MonteCarlo (speedup) → updated: parallelization-models.md, distributed-computing-tradeoffs.md; new: sources/demo-strategy.md
## [2026-04-18] ingest | modules/TSP/README.md (Ukrainian) — module config, file-input formats, test-data patterns → updated: tsp-module.md; new: sources/tsp-module-readme.md
