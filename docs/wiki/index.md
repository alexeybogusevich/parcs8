---
title: Wiki Index
type: index
status: unreviewed
updated: 2026-04-18
---

# PARCS Knowledge Wiki — Index

Entry point for the project's LLM-built knowledge base. For anything new, **read this first** — it maps raw sources → wiki pages.

The corpus centres on two intertwined topics:

1. **The PARCS distributed computing system** — a .NET + Kubernetes platform for running parallel "modules" across daemon workers.
2. **The TSP Module** — a parallel genetic algorithm for the Traveling Salesman Problem that serves as the reference workload, case study, and benchmark for the system.

---

## Systems & Architecture

- [PARCS System Overview](parcs-system.md) — Host / Daemon / Portal architecture, Kubernetes deployment, KEDA autoscaling

## Algorithms & Models

- [Genetic Algorithm for TSP](genetic-algorithm.md) — chromosome representation, OX crossover, swap mutation, tournament selection, elitism, early stopping
- [Parallelization Models](parallelization-models.md) — Sequential vs Island Model vs Island+Migration vs Master-Slave; when each wins

## The TSP Module

- [TSP Module](tsp-module.md) — module structure (Sequential / Parallel / Worker), configuration, input formats, test-data patterns

## Results & Analysis

- [TSP Benchmarks](tsp-benchmarks.md) — 500/1000/2000-city results, migration wins, anomalies flagged in raw data
- [Distributed Computing Tradeoffs](distributed-computing-tradeoffs.md) — speedup vs efficiency, when parallel helps, threshold for Master-Slave viability

## Publications

- [HAIT Article (2024)](hait-article.md) — Bogusevych, *Parallel Genetic Algorithm for TSP in PARCS* — English + Ukrainian versions, critique, formatting rules

---

## Sources

### Ingested (summary page written)

- [Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md](../raw/documents/Parallel_Genetic_Algorithm_for_TSP_in_PARCS_System.md) → [summary](sources/parallel-ga-paper.md)
- [HAIT_Article_TSP_Parallel_Genetic_Algorithm.md](../raw/documents/documents/HAIT_Article_TSP_Parallel_Genetic_Algorithm.md) → [summary](sources/hait-article-english.md)
- [HAIT_Article_TSP_Ukrainian.md](../raw/documents/documents/HAIT_Article_TSP_Ukrainian.md) → [summary](sources/hait-article-ukrainian.md)
- [Article_Critique.md](../raw/documents/Article_Critique.md) → [summary](sources/article-critique.md)
- [TSP_Results_Analysis.md](../raw/documents/TSP_Results_Analysis.md) → [summary](sources/tsp-results-analysis.md)
- [TSP_Results_Executive_Summary.md](../raw/documents/TSP_Results_Executive_Summary.md) → [summary](sources/tsp-results-executive.md)
- [MasterSlave_Performance_Analysis.md](../raw/documents/MasterSlave_Performance_Analysis.md) → [summary](sources/master-slave-performance.md)
- [MasterSlave_Optimizations.md](../raw/documents/MasterSlave_Optimizations.md) → [summary](sources/master-slave-optimizations.md)
- [TSP_Parallel_Speed_vs_Efficiency.md](../raw/documents/TSP_Parallel_Speed_vs_Efficiency.md) → [summary](sources/speed-vs-efficiency.md)
- [Distributed_Computing_Demonstration_Strategy.md](../raw/documents/Distributed_Computing_Demonstration_Strategy.md) → [summary](sources/demo-strategy.md)
- [modules/…/README.md (TSP)](../raw/modules/Parcs.Modules.TravelingSalesman/README.md) → [summary](sources/tsp-module-readme.md)

### Pending ingest

English markdown:
- [TSP_Detailed_Explanation.md](../raw/documents/TSP_Detailed_Explanation.md) — GA walkthrough with code and example
- [TSP_Complete_Example.md](../raw/documents/TSP_Complete_Example.md) — 5-city step-by-step trace
- [TSP_Module_Analysis.md](../raw/documents/TSP_Module_Analysis.md) — OX-crossover bug report
- [TSP_Module_Fixes_Summary.md](../raw/documents/TSP_Module_Fixes_Summary.md) — fix records for OX + pop-size + validation
- [modules/…/ISLAND_MODEL_BENEFITS.md](../raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_BENEFITS.md)
- [modules/…/ISLAND_MODEL_MIGRATION_README.md](../raw/modules/Parcs.Modules.TravelingSalesman/ISLAND_MODEL_MIGRATION_README.md)
- [modules/…/MASTER_SLAVE_README.md](../raw/modules/Parcs.Modules.TravelingSalesman/MASTER_SLAVE_README.md)
- [modules/…/MIGRATION_README.md](../raw/modules/Parcs.Modules.TravelingSalesman/MIGRATION_README.md)
- [modules/…/NEW_FEATURES_SUMMARY.md](../raw/modules/Parcs.Modules.TravelingSalesman/NEW_FEATURES_SUMMARY.md)
- [documents/modules/…/README.md](../raw/documents/modules/Parcs.Modules.TravelingSalesman/README.md)
- [documents/modules/…/Examples/README.md](../raw/documents/modules/Parcs.Modules.TravelingSalesman/Examples/README.md)
- [HAIT_Article_Ukrainian_Metadata.md](../raw/documents/documents/HAIT_Article_Ukrainian_Metadata.md) — author metadata, mostly citation info
- [HAIT_Formatting_Instructions.md](../raw/documents/documents/HAIT_Formatting_Instructions.md) — HAIT journal style guide

Binary sources (need pdf/docx skill to extract):
- `raw/documents/Leveraging K8s to implement PARCS.NET.pdf` — English PDF
- `raw/parcs_gcp_guide.docx`, `raw/parcs_mcp_guide.docx`, `raw/presentation_plan.docx`, `raw/Стаття (AutoRecovered).docx`
- `raw/documents/Богусевич_маг.роб.pdf` — Master's thesis
- `raw/documents/Резервування в задачі моделювання паралельних обчислень засобами ПАРКС K8s.pdf`
- `raw/documents/Богусевич - Іспит - …docx`, `raw/documents/Дослідницька_Пропозиція.docx`, `raw/documents/ЗВІТ ПРО ВПРОВАДЖЕННЯ …docx`, `raw/documents/Стаття (AutoRecovered).docx`

---

## Conventions

See [.claude/skills/llm-wiki/SKILL.md](../../.claude/skills/llm-wiki/SKILL.md) for frontmatter, filename, and workflow rules. Notable:

- `status: validated` in a page's frontmatter = human-locked, do not modify.
- Cite every claim with `[source: raw/…]` or `[wiki: page.md]`.
- One source per ingest operation; update `log.md` on each.
