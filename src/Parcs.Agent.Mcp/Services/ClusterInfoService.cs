using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// Queries the Kubernetes API to report live cluster capacity.
/// Computes max parallelism as: floor(allocatable_cpu_per_node / daemon_cpu_request) × worker_nodes
/// </summary>
public sealed class ClusterInfoService
{
    // Resource request per daemon pod (from deployment.azure.yaml)
    private const double DaemonCpuRequestMillicores = 250.0;

    private readonly ILogger<ClusterInfoService> _logger;
    private readonly IConfiguration _config;
    private Kubernetes? _k8s;

    public ClusterInfoService(ILogger<ClusterInfoService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration;
    }

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

            // Worker nodes: schedulable nodes with the agent workload label
            // (excludes control-plane nodes which are tainted NoSchedule)
            var workerNodes = nodes.Items
                .Where(n => !HasControlPlaneTaint(n) && n.Spec?.Unschedulable != true)
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

            return new ClusterInfoResult
            {
                WorkerNodeCount = workerNodes.Count,
                MaxParallelism  = (int)totalMaxDaemons,
                DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Kubernetes API, returning fallback capacity");
            return new ClusterInfoResult
            {
                WorkerNodeCount = _config.GetValue<int>("Parcs:FallbackWorkerNodeCount", 19),
                MaxParallelism  = _config.GetValue<int>("Parcs:FallbackMaxParallelism", 133),
                DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
            };
        }
    }

    private static bool HasControlPlaneTaint(V1Node node) =>
        node.Spec?.Taints?.Any(t =>
            t.Key is "node-role.kubernetes.io/control-plane"
                  or "node-role.kubernetes.io/master") == true;

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
    public int MaxParallelism { get; init; }
    public int DaemonCpuRequestMillicores { get; init; }
}
