---
title: "Source: MasterSlave_Optimizations.md"
type: summary
status: unreviewed
sources:
  - raw/documents/MasterSlave_Optimizations.md
updated: 2026-04-18
---

# Source summary — Master-Slave Optimizations

Follow-up to `MasterSlave_Performance_Analysis.md`. Reports optimisations applied and expected impact.

## Changes landed

1. **Binary serialization** (`BinaryWriter` / `BinaryReader`) replacing JSON — expected 3–5× faster. Payload format: `[numRoutes][len][data][len][data]…`.
2. **Reduced allocations in hot path** — pre-sized `List<double>`, cached route length, LINQ removed. ~10–20% faster fitness calculation.
3. **Efficient data structures** — direct array access instead of `List<List<int>>` + LINQ.

## Expected impact (2000 cities / 1000 population)

| Stage | JSON | Binary |
|---|---|---|
| Serialization | ~100 s | ~20 s |
| Network | ~200 s | ~80 s |
| Deserialization | ~100 s | ~20 s |
| **Total comms** | ~400 s | **~120 s (−70%)** |
| Parallel fitness | ~156 s | ~150 s |
| **Total** | **~566 s** | **~270 s (−52%)** |

Still 1.5× slower than sequential (177 s) at 2000 cities. Only at ≥5000 cities does optimised Master-Slave clearly win.

## Future optimisations noted (not applied)

- Compression (gzip/zstd) on binary payload.
- Batched generations (workers do N generations locally between syncs).
- Delta encoding (only send changed routes).
- Memory-mapped channels (zero-copy local).

## Recommended thresholds (post-optimization)

- <3000 cities → use sequential or Island.
- 3000–5000 cities → parity; Island still better for quality.
- ≥5000 cities → optimised Master-Slave expected 1.5–2× speedup.

## Feeds wiki pages

- [Parallelization Models](../parallelization-models.md) (Master-Slave break-even)
- [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md)
