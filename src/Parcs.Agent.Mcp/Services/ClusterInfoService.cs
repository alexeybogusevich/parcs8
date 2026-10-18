using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// Queries the Kubernetes API to report live cluster capacity.
/// Current capacity is the sum over daemon-eligible nodes of floor(allocatable_cpu / daemon_cpu_request);
/// nodes with a NoSchedule/NoExecute taint (control plane, GPU pools) or labelled pool=system are excluded.
/// Max parallelism is the ScaledJob maxReplicaCount when configured (nodes are added on demand up to it),
/// otherwise the current capacity.
/// </summary>
public sealed class ClusterInfoService
{
    private readonly ILogger<ClusterInfoService> _logger;
    private readonly IConfiguration _config;
    private Kubernetes? _k8s;

    public ClusterInfoService(ILogger<ClusterInfoService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration;
    }

    // Resource request per daemon pod; must match the daemon ScaledJob in kube/deployment.*.yaml
    private double DaemonCpuRequestMillicores => _config.GetValue("Parcs:DaemonCpuRequestMillicores", 250.0);

    // maxReplicaCount of the daemon ScaledJob; 0 means not configured
    private int MaxReplicaCount => _config.GetValue("Parcs:MaxReplicaCount", 0);

    private Kubernetes GetClient()
    {
        if (_k8s is not null) return _k8s;

        KubernetesClientConfiguration cfg;
        try
        {
            cfg = KubernetesClientConfiguration.InClusterConfig();
        }
        catch
        {
            // Running outside cluster (local dev) — fall back to kubeconfig
            cfg = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        }

        _k8s = new Kubernetes(cfg);
        return _k8s;
    }

    public async Task<ClusterInfoResult> GetClusterInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var client = GetClient();
            var nodes  = await client.ListNodeAsync(cancellationToken: ct);

            var workerNodes = nodes.Items
                .Where(n => !HasBlockingTaint(n) && !IsSystemNode(n) && n.Spec?.Unschedulable != true)
                .ToList();

            double totalMaxDaemons = 0;
            foreach (var node in workerNodes)
            {
                if (node.Status?.Allocatable?.TryGetValue("cpu", out var cpuQuantity) == true)
                {
                    var allocatableMillicores = ParseCpuToMillicores(cpuQuantity.Value);
                    totalMaxDaemons += Math.Floor(allocatableMillicores / DaemonCpuRequestMillicores);
                }
            }

            var currentCapacity = (int)totalMaxDaemons;
            return new ClusterInfoResult
            {
                WorkerNodeCount = workerNodes.Count,
                CurrentCapacity = currentCapacity,
                MaxParallelism  = MaxReplicaCount > 0 ? MaxReplicaCount : currentCapacity,
                DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Kubernetes API, returning fallback capacity");
            return new ClusterInfoResult
            {
                WorkerNodeCount = _config.GetValue<int>("Parcs:FallbackWorkerNodeCount", 19),
                CurrentCapacity = _config.GetValue<int>("Parcs:FallbackMaxParallelism", 133),
                MaxParallelism  = _config.GetValue<int>("Parcs:FallbackMaxParallelism", 133),
                DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
            };
        }
    }

    // Daemons carry no tolerations, so any NoSchedule/NoExecute taint (control plane, GPU pool) excludes the node
    private static bool HasBlockingTaint(V1Node node) =>
        node.Spec?.Taints?.Any(t => t.Effect is "NoSchedule" or "NoExecute") == true;

    // The EKS system node group (pool=system) hosts the control services, not daemons
    private static bool IsSystemNode(V1Node node) =>
        node.Metadata?.Labels?.TryGetValue("pool", out var pool) == true && pool == "system";

    private static double ParseCpuToMillicores(string value)
    {
        if (value.EndsWith('m'))
            return double.Parse(value[..^1]);
        // Whole cores
        return double.Parse(value) * 1000;
    }
}

public sealed class ClusterInfoResult
{
    public int WorkerNodeCount { get; init; }
    public int CurrentCapacity { get; init; }
    public int MaxParallelism { get; init; }
    public int DaemonCpuRequestMillicores { get; init; }
}
