# Scaling modes: elastic and static

PARCS allocates points in one of two modes, chosen by the `Scaling__Mode` environment variable. Switching needs no rebuild.

| | **Elastic** (default in the cloud manifests) | **Static** |
|---|---|---|
| Where a point runs | A new daemon pod per point. The Host (or a daemon, for nested points) publishes a request to the point queue (Pub/Sub, Service Bus or SQS); KEDA starts a Job pod and the node autoscaler (Cluster Autoscaler or Karpenter) adds nodes when needed | A pre-started daemon in the `parcs-daemon-static` Deployment, found through the `parcs-daemon-headless` Service |
| Provisioning during a run | Pod start, and node start for points that do not fit (tens of seconds to minutes) | None |
| Placement | One point per pod | Least-loaded daemon first, across all jobs of the Host (`DaemonAllocator`) |
| MCP `get_cluster_info` | `scalingMode: "elastic"`, `maxParallelism` = ScaledJob `maxReplicaCount` | `scalingMode: "static"`, `maxParallelism` = Ready pool pods × `Scaling__PointsPerDaemon` (default 1), `capacitySource: "dns"` |
| Use for | Production, elasticity experiments | Controlled experiments (fixed resources, no VM spin-up), debugging, local runs |

If `Scaling__Mode` is unset, the Host and the daemons follow the point queue: Elastic when a queue provider is configured, Static otherwise. This is how `docker-compose` and `kube/deployment.local.yaml` run. The MCP server has no queue configuration, so it defaults to Elastic. Set `Scaling__Mode` on the MCP server to the same value as on the Host.

## Switching a cloud cluster to static

The static pool ships in every cloud manifest with `replicas: 0`, so it does nothing until you scale it.

```bash
# 1. Start the pool and wait until every pod is Ready. Each daemon requests = limits 1 CPU / 2 GiB
#    (guaranteed QoS, no CPU sharing between points). On first use the node autoscaler may add
#    nodes for the pool; that happens here, before any run, never during one.
kubectl scale deployment parcs-daemon-static --replicas=8
kubectl rollout status deployment parcs-daemon-static

# 2. Route points to the pool. This restarts the Host and the MCP server, and in-memory MCP
#    sessions are lost, so do it between experiments.
kubectl set env deployment/parcs-hostapi deployment/parcs-agent-mcp Scaling__Mode=Static

# 3. Optional: pause the ScaledJobs. No point requests are published in static mode, so they stay
#    idle anyway.
kubectl annotate scaledjob parcs-daemon-scaler autoscaling.keda.sh/paused=true --overwrite
```

To keep the node pool itself fixed as well, set its minimum and maximum node count to the same value in Terraform or Bicep (GKE/AKS node pool autoscaling, or the Karpenter NodePool limits on EKS).

## Switching back to elastic

```bash
kubectl set env deployment/parcs-hostapi deployment/parcs-agent-mcp Scaling__Mode=Elastic
kubectl annotate scaledjob parcs-daemon-scaler autoscaling.keda.sh/paused-
kubectl scale deployment parcs-daemon-static --replicas=0
```

## Notes

- Pool size bounds parallelism. With `Scaling__PointsPerDaemon=1`, the MCP server rejects layers wider than the pool (`capacity_exceeded`) and queues layers that do not fit next to running ones. The Host itself does not reject: with more points than daemons, points share daemons.
- Nested points created on a static daemon go to the pool through that daemon's own allocator. They spread evenly within that daemon's job, not across the whole cluster.
- GPU jobs (`RequiresGpu`) are not routed to a separate pool in static mode. GPU modules fall back to their CPU path when the daemon has no GPU.
- The Host's headless Service lists only Ready pods, so a pod that is still starting receives no points.
