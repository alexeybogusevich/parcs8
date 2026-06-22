---
title: Distributed Computing Tradeoffs
type: concept
status: unreviewed
sources:
  - raw/documents/TSP_Parallel_Speed_vs_Efficiency.md
  - raw/documents/MasterSlave_Performance_Analysis.md
  - raw/documents/MasterSlave_Optimizations.md
  - raw/documents/Distributed_Computing_Demonstration_Strategy.md
updated: 2026-04-18
---

# Distributed Computing Tradeoffs

The corpus contains several near-collisions where "parallel is better" is true in one sense and false in another. This page is the reference for which question to ask.

## Wall-clock speed ≠ compute efficiency

**Speedup** = sequential time / parallel time. Measures "how long you wait".
**Efficiency** = speedup / worker count. Measures "how well you used the extra cores".
**Total work** = workers × parallel time. Measures "how much compute you burned".

Example (50 cities): sequential 45.7 s, parallel 12.1 s on 4 workers.
- Speedup = 3.78× ✅
- Efficiency = 94.5% (very good)
- Total work = 4 × 12.1 = 48.4 CPU-seconds — **more** than sequential's 45.7 CPU-seconds.

So "parallel is faster" is true. "Parallel uses less compute" is **false** — it uses ~6% more due to coordination overhead. In a cloud setting where you pay per CPU-hour, parallel is slightly *more* expensive. `[source: raw/documents/TSP_Parallel_Speed_vs_Efficiency.md]`

## When parallel speedup actually appears

The [TSP Module](tsp-module.md) has three parallel models. Only one of them produces real wall-clock speedup, and only under specific conditions.

- **Island Model** — no speedup. Same wall-clock time, better solution quality.
- **Island + Migration** — modest slowdown (13–25%), much better quality.
- **Master-Slave** — *attempts* speedup by parallelising fitness evaluation, but only wins when problem is large enough for fitness-eval to dwarf comms:

### Break-even conditions for Master-Slave

At 2000 cities / 1000 routes:
```
Sequential:  ~177 s
Master-Slave: ~566 s  →  3.2× SLOWER
  serialization  ~100 s
  network        ~200 s
  deserialization ~100 s
  parallel fitness ~156 s  (only 11 s saved over sequential)
```

`[source: raw/documents/MasterSlave_Performance_Analysis.md]`

Binary serialization (replacing JSON) cuts communication from ~400 s → ~120 s at 2000 cities — still ~270 s total, still 1.5× slower than sequential. Only at **10 000 cities / 2000 population** does optimised Master-Slave hit ~2.3× speedup. `[source: raw/documents/MasterSlave_Optimizations.md]`

Practical thresholds for Master-Slave:
- <3000 cities → don't use it. Sequential or Island wins.
- 3000–5000 cities → optimised (binary) Master-Slave at parity with sequential.
- ≥5000 cities → 1.5–2× speedup.

## When to demo speedup vs quality

If the goal is "demonstrate distributed computing", `[source: raw/documents/Distributed_Computing_Demonstration_Strategy.md]` recommends:

- **To show quality gain**: TSP Island Model — clean story ("4 independent searches → better answer in the same time").
- **To show raw speedup**: pick an embarrassingly parallel workload. `Parcs.Modules.ProofOfWork` (nonce search) and `Parcs.Modules.MonteCarloPi` (independent samples) both hit near-linear speedup because each worker does independent work with zero cross-talk.

Reframe the usual pitch: *"Parallel computing makes better decisions, not just faster ones"* — because for most optimisation workloads, that's the real lever.

## Overhead sources (ranked)

1. **Serialization/deserialization** — JSON is ~3–5× slower than binary; biggest single cost in Master-Slave.
2. **Network transfer** — scales with payload; binary + compression can cut 50–70%.
3. **Load imbalance** — one slow worker gates the batch.
4. **Startup / cold-start** — KEDA-scaled daemon pods on AKS cluster autoscaler can take 3–5 minutes to come online. Only matters for short jobs. `[source: raw/documents/Article_Critique.md]`

## Related wiki pages

- [Parallelization Models](parallelization-models.md) — model-by-model
- [TSP Benchmarks](tsp-benchmarks.md) — measured numbers at 500/1000/2000 cities
- [PARCS System](parcs-system.md) — the platform this all runs on
