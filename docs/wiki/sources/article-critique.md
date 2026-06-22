---
title: "Source: Article_Critique.md"
type: summary
status: unreviewed
sources:
  - raw/documents/Article_Critique.md
updated: 2026-04-18
---

# Source summary — Article Critique

Structured reviewer notes on the HAIT submission ("Building Highly Scalable Parallel Compute Systems with PARCS, Kubernetes and KEDA").

## Strengths (per reviewer)

1. Clear problem statement — manual-scaling limitation of PARCS-Kubernetes.
2. Well-documented architecture with KEDA + Service Bus, flow diagram.
3. Production-ready integration (KEDA + AKS Cluster Autoscaler).
4. Real-world TSP case study.

## Gaps flagged (actionable for revision)

1. **Missing implementation detail** — no Service Bus publish code, no daemon consume code, no retry/error handling, no message schema, no KEDA `ScaledObject` YAML, no daemon lifecycle section.
2. **Incomplete related work** — no comparison table across PARCS-NET / PARCS-WCF / PARCS-Kubernetes; no discussion of Knative, Fargate+EventBridge.
3. **Missing performance analysis** — no cold-start time, no scale-from-0-to-N latency, no cost comparison with always-on daemons, no Service Bus message-cost analysis.
4. **No challenges/limitations section** — cold start, message ordering + exactly-once, poison messages, node provisioning (AKS 3–5 min), frequent-pod-churn cost.
5. **No security section** — Host↔Daemon authN, Service Bus secrets, network policies.

## Feeds wiki pages

- [HAIT Article](../hait-article.md) (primary — revision checklist)
- [PARCS System](../parcs-system.md) (gaps listed as "observed gaps")
- [Distributed Computing Tradeoffs](../distributed-computing-tradeoffs.md) (cold-start overhead)
