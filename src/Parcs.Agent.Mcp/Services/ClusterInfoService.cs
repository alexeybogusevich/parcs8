using System.Globalization;
using System.Net;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

public interface IClusterInfoService
{
    Task<ClusterInfoResult> GetClusterInfoAsync(CancellationToken ct = default);
}

/// <summary>
/// Reports how many daemon workers a layer can use, in one of two scaling modes (<c>Scaling:Mode</c>):
///
/// Elastic (default) — KEDA starts one daemon pod per point and the node autoscaler adds nodes.
/// Current capacity is the sum over daemon-eligible nodes of floor(allocatable_cpu / daemon_cpu_request);
/// nodes with a NoSchedule/NoExecute taint (control plane, GPU pools) or labelled pool=system are excluded.
/// Max parallelism is the ScaledJob maxReplicaCount when configured (nodes are added on demand up to it),
/// otherwise the current capacity.
///
/// Static — a fixed pool of pre-started daemons behind a headless Service; nothing is provisioned.
/// Capacity is the number of ready daemons (DNS records of the Service) × <c>Scaling:PointsPerDaemon</c>.
/// </summary>
public sealed class ClusterInfoService : IClusterInfoService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly ILogger<ClusterInfoService> _logger;
    private readonly IConfiguration _config;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;
    private Kubernetes? _k8s;
    private ClusterInfoResult? _cached;
    private DateTimeOffset _cachedAt;

    public ClusterInfoService(
        ILogger<ClusterInfoService> logger,
        IConfiguration configuration,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
    {
        _logger = logger;
        _config = configuration;
        _resolveHost = resolveHost ?? Dns.GetHostAddressesAsync;
    }

    public string ScalingMode => ScalingModes.Resolve(_config);

    // Resource request per daemon pod; must match the daemon ScaledJob / Deployment in kube/deployment.*.yaml
    private double DaemonCpuRequestMillicores => _config.GetValue("Parcs:DaemonCpuRequestMillicores", 250.0);

    // maxReplicaCount of the daemon ScaledJob; 0 means not configured
    private int MaxReplicaCount => _config.GetValue("Parcs:MaxReplicaCount", 0);

    private int PointsPerDaemon => Math.Max(1, _config.GetValue("Scaling:PointsPerDaemon", 1));

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
        var cached = _cached;
        if (cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
            return cached;

        var result = ScalingMode == ScalingModes.Static
            ? await GetStaticPoolInfoAsync(ct)
            : await GetElasticInfoAsync(ct);

        _cached   = result;
        _cachedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task<ClusterInfoResult> GetElasticInfoAsync(CancellationToken ct)
    {
        try
        {
            var workerNodes = await ListDaemonNodesAsync(ct);

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
                ScalingMode     = ScalingModes.Elastic,
                CapacitySource  = CapacitySources.Kubernetes,
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
                ScalingMode     = ScalingModes.Elastic,
                CapacitySource  = CapacitySources.Fallback,
                WorkerNodeCount = _config.GetValue<int>("Parcs:FallbackWorkerNodeCount", 19),
                CurrentCapacity = _config.GetValue<int>("Parcs:FallbackMaxParallelism", 133),
                MaxParallelism  = _config.GetValue<int>("Parcs:FallbackMaxParallelism", 133),
                DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
            };
        }
    }

    private async Task<ClusterInfoResult> GetStaticPoolInfoAsync(CancellationToken ct)
    {
        var (daemons, source) = await CountStaticDaemonsAsync(ct);

        int workerNodeCount;
        try
        {
            workerNodeCount = (await ListDaemonNodesAsync(ct)).Count;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kubernetes API unavailable; node count not reported");
            workerNodeCount = 0;
        }

        var capacity = daemons * PointsPerDaemon;
        return new ClusterInfoResult
        {
            ScalingMode     = ScalingModes.Static,
            CapacitySource  = source,
            WorkerNodeCount = workerNodeCount,
            CurrentCapacity = capacity,
            MaxParallelism  = capacity,
            DaemonCpuRequestMillicores = (int)DaemonCpuRequestMillicores,
        };
    }

    /// <summary>
    /// Ready daemons of the static pool: the A records of the headless Service, which lists only
    /// ready pods. Falls back to <c>Scaling:StaticDaemonCount</c> outside Kubernetes.
    /// </summary>
    private async Task<(int Count, string Source)> CountStaticDaemonsAsync(CancellationToken ct)
    {
        var service = _config["Kubernetes:DaemonsHeadlessServiceName"];
        if (!string.IsNullOrWhiteSpace(service))
        {
            var ns   = _config["Kubernetes:NamespaceName"];
            var host = string.IsNullOrWhiteSpace(ns) ? service : $"{service}.{ns}";
            try
            {
                var addresses = await _resolveHost(host, ct);
                return (addresses.Length, CapacitySources.Dns);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve static daemon service {Host}", host);
            }
        }

        var configured = _config.GetValue<int?>("Scaling:StaticDaemonCount");
        return configured is > 0
            ? (configured.Value, CapacitySources.Configuration)
            : (_config.GetValue<int>("Parcs:FallbackMaxParallelism", 1), CapacitySources.Fallback);
    }

    private async Task<List<V1Node>> ListDaemonNodesAsync(CancellationToken ct)
    {
        var nodes = await GetClient().ListNodeAsync(cancellationToken: ct);
        return nodes.Items
            .Where(n => !HasBlockingTaint(n) && !IsSystemNode(n) && n.Spec?.Unschedulable != true)
            .ToList();
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
            return double.Parse(value[..^1], CultureInfo.InvariantCulture);
        // Whole cores
        return double.Parse(value, CultureInfo.InvariantCulture) * 1000;
    }
}

public sealed class ClusterInfoResult
{
    public string ScalingMode { get; init; } = ScalingModes.Elastic;
    public string CapacitySource { get; init; } = CapacitySources.Kubernetes;
    public int WorkerNodeCount { get; init; }
    public int CurrentCapacity { get; init; }
    public int MaxParallelism { get; init; }
    public int DaemonCpuRequestMillicores { get; init; }
}

public static class ScalingModes
{
    public const string Elastic = "elastic";
    public const string Static = "static";

    /// <summary>Reads <c>Scaling:Mode</c> (Elastic | Static, case-insensitive); Elastic when unset.</summary>
    public static string Resolve(IConfiguration configuration) =>
        configuration["Scaling:Mode"]?.Trim().ToLowerInvariant() switch
        {
            null or "" or Elastic => Elastic,
            Static => Static,
            var other => throw new InvalidOperationException($"Unknown Scaling:Mode '{other}'. Use Elastic or Static."),
        };
}

public static class CapacitySources
{
    /// <summary>Computed from the nodes reported by the Kubernetes API (elastic mode).</summary>
    public const string Kubernetes = "kubernetes";
    /// <summary>Counted from the DNS records of the static daemons' headless Service.</summary>
    public const string Dns = "dns";
    /// <summary>Taken from Scaling:StaticDaemonCount.</summary>
    public const string Configuration = "configuration";
    /// <summary>A configured guess used because the real capacity could not be read.</summary>
    public const string Fallback = "fallback";
}
