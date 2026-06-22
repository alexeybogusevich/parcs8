---
title: "Source: Distributed_Computing_Demonstration_Strategy.md"
type: summary
status: unreviewed
sources:
  - raw/documents/Distributed_Computing_Demonstration_Strategy.md
updated: 2026-04-18
---

# Source summary — Distributed Computing Demonstration Strategy

Recommends how to *pitch* distributed computing using PARCS workloads — given that TSP Master-Slave doesn't cleanly demonstrate speedup at realistic sizes.

## Core pitch reframe

**Instead of**: "Parallel computing makes things faster."
**Use**: "Parallel computing finds BETTER solutions by exploring more of the solution space simultaneously."

## Recommended demo workloads

### Option 1 (primary) — TSP Island Model for *quality*

Clean narrative: 4 independent searches → best-of-4 beats single run at the same wall-clock time. No communication overhead issues. Real-world pattern (ensembles).

### Option 2 — Embarrassingly parallel workloads for *speed*

For "show 4× speedup" moments, don't use TSP — use:

- **`Parcs.Modules.ProofOfWork`** — nonce search, linear speedup expected.
- **`Parcs.Modules.MonteCarloPi`** — independent samples, linear speedup expected.
- **`Parcs.Modules.MatrixesMultiplication`** — block decomposition, ~3.5× on 4 workers.

All three already exist in the repo under `modules/`.

### Option 3 — Hybrid demonstration

Run both TSP (Island quality story) and ProofOfWork/MonteCarlo (speedup story) side by side. Covers both value propositions.

## Why TSP Master-Slave doesn't work for demos

Covered in detail in [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md): comms dominates for problems <5000 cities, and that's the range people actually care about.

## Feeds wiki pages

- [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md)
- [Parallelization Models](../parallelization-models.md) ("Practical guidance")
