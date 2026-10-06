using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Messaging;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Core.Services
{
    /// <summary>
    /// Creates points by publishing requests to the point queue: KEDA provisions one daemon pod
    /// per request (and the cluster autoscaler adds nodes when needed), and each pod dials back
    /// to the parent's <see cref="CallbackTcpServer"/>. The parent may be the Host or a daemon,
    /// so modules running on daemons can create nested points the same way.
    /// </summary>
    public sealed class QueuePointCreationService(
        IPointRequestPublisher publisher,
        IJobPlacementResolver jobPlacementResolver,
        CallbackTcpServer callbackTcpServer,
        IOptions<HostTcpConfiguration> hostTcpOptions,
        IArgumentsProviderFactory argumentsProviderFactory,
        ILogger<QueuePointCreationService> logger) : IPointCreationService
    {
        private readonly IPointRequestPublisher _publisher = publisher;
        private readonly IJobPlacementResolver _jobPlacementResolver = jobPlacementResolver;
        private readonly CallbackTcpServer _callbackTcpServer = callbackTcpServer;
        private readonly HostTcpConfiguration _hostTcpConfiguration = hostTcpOptions.Value;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory = argumentsProviderFactory;
        private readonly ILogger<QueuePointCreationService> _logger = logger;

        public bool IsEnabled => _publisher.IsEnabled;

        public async Task<IPoint> CreatePointAsync(long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default)
        {
            var points = await CreatePointsAsync(1, jobId, moduleId, arguments, cancellationToken);
            return points[0];
        }

        public async Task<IPoint[]> CreatePointsAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default)
        {
            var requiresGpu = await _jobPlacementResolver.RequiresGpuAsync(jobId, cancellationToken);
            var (callbackAddress, callbackPort) = _callbackTcpServer.GetAdvertisedEndpoint();

            // Phase 1 — register every pending connection slot before publishing, so a fast
            // daemon cannot connect before its slot exists, then publish the whole batch.
            var requests = new PointCreationRequest[count];
            var connectionTasks = new Task<NetworkChannel>[count];

            for (int i = 0; i < count; i++)
            {
                var correlationId = Guid.NewGuid().ToString();
                connectionTasks[i] = _callbackTcpServer.WaitForConnectionAsync(correlationId, cancellationToken);

                requests[i] = new PointCreationRequest
                {
                    JobId = jobId,
                    ModuleId = moduleId,
                    Arguments = arguments,
                    HostUrl = callbackAddress,
                    HostPort = callbackPort,
                    CorrelationId = correlationId,
                    RequiresGpu = requiresGpu,
                    RequestedAt = DateTimeOffset.UtcNow,
                };
            }

            var startedAt = DateTimeOffset.UtcNow;
            await _publisher.PublishAsync(requests, requiresGpu, cancellationToken);

            _logger.LogInformation(
                "Published {Count} {Pool} point requests for job {JobId}; awaiting daemon connections on {Address}:{Port}",
                count, requiresGpu ? "GPU" : "CPU", jobId, callbackAddress, callbackPort);

            // Phase 2 — await all daemon connections concurrently, bounded by a timeout. Without
            // it, one daemon that never connects (lost message, pod never scheduled, duplicate
            // delivery race) hangs this call forever, since nothing downstream times out either.
            var connectTimeout = TimeSpan.FromSeconds(_hostTcpConfiguration.DaemonConnectTimeoutSeconds);
            var allConnected = Task.WhenAll(connectionTasks);
            var completed = await Task.WhenAny(allConnected, Task.Delay(connectTimeout, cancellationToken));

            if (completed != allConnected)
            {
                var connectedCount = connectionTasks.Count(t => t.IsCompletedSuccessfully);
                throw new TimeoutException(
                    $"Timed out after {connectTimeout.TotalSeconds}s waiting for daemons to connect for job " +
                    $"{jobId}: {connectedCount}/{count} connected.");
            }

            var channels = await allConnected;

            // Provisioning latency of the whole batch: broker → KEDA → pod (→ node) → callback.
            _logger.LogInformation(
                "All {Count} daemons connected for job {JobId} in {ProvisioningSeconds:F2}s",
                count, jobId, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);

            // Phase 3 — build Point instances from the resolved channels.
            var points = new IPoint[count];
            for (int i = 0; i < count; i++)
            {
                channels[i].SetCancellation(cancellationToken);
                var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                points[i] = new Point(jobId, moduleId, channels[i], argumentsProvider);
            }

            return points;
        }
    }
}
