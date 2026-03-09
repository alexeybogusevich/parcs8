using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using Parcs.Net;

namespace Parcs.Host.Services
{
    public sealed class PointCreationService(
        IOptions<ServiceBusConfiguration> serviceBusOptions,
        IOptions<HostTcpConfiguration> hostTcpOptions,
        IAddressResolver addressResolver,
        IInternalChannelManager internalChannelManager,
        IArgumentsProviderFactory argumentsProviderFactory,
        HostedServices.HostTcpServer hostTcpServer,
        ILogger<PointCreationService> logger) : IPointCreationService
    {
        private readonly ServiceBusConfiguration _serviceBusConfiguration = serviceBusOptions.Value;
        private readonly HostTcpConfiguration _hostTcpConfiguration = hostTcpOptions.Value;
        private readonly IAddressResolver _addressResolver = addressResolver;
        private readonly IInternalChannelManager _internalChannelManager = internalChannelManager;
        private readonly IArgumentsProviderFactory _argumentsProviderFactory = argumentsProviderFactory;
        private readonly HostedServices.HostTcpServer _hostTcpServer = hostTcpServer;
        private readonly ILogger<PointCreationService> _logger = logger;

        public async Task<IPoint> CreatePointAsync(long jobId, long moduleId, IDictionary<string, string> arguments, string daemonHostUrl, int daemonPort, CancellationToken cancellationToken = default)
        {
            var points = await CreatePointsAsync(1, jobId, moduleId, arguments, daemonHostUrl, daemonPort, cancellationToken);
            return points[0];
        }

        /// <summary>
        /// Publishes <paramref name="count"/> point-requested messages to Service Bus in one pass,
        /// registers all pending TCP connection slots, then awaits all daemon connections
        /// concurrently. KEDA therefore sees the full queue depth at once and can provision daemon
        /// pods (and, if needed, new cluster nodes via AKS CA) in parallel — matching Fig. 2 of
        /// the article.
        /// </summary>
        public async Task<IPoint[]> CreatePointsAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default)
        {
            return await CreatePointsAsync(count, jobId, moduleId, arguments, null, 0, cancellationToken);
        }

        private async Task<IPoint[]> CreatePointsAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, string daemonHostUrl, int daemonPort, CancellationToken cancellationToken)
        {
            // --- Service Bus / KEDA path ---
            if (!string.IsNullOrEmpty(_serviceBusConfiguration.ConnectionString) &&
                !string.IsNullOrEmpty(_serviceBusConfiguration.QueueName))
            {
                return await CreatePointsViaServiceBusAsync(count, jobId, moduleId, arguments, cancellationToken);
            }

            // --- Loopback / internal path (dev / unit-test) ---
            if (daemonHostUrl != null)
            {
                var daemonAddresses = _addressResolver.Resolve(daemonHostUrl);
                if (daemonAddresses.Any(IPAddress.IsLoopback))
                {
                    var internalPoints = new IPoint[count];
                    for (int i = 0; i < count; i++)
                    {
                        var internalChannelId = _internalChannelManager.Create();
                        _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannelPair);
                        var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                        internalPoints[i] = new Point(jobId, moduleId, internalChannelPair.Item1, argumentsProvider);
                    }
                    return internalPoints;
                }
            }

            // --- Legacy direct TCP path (non-K8s, pre-KEDA) ---
            var maxRetries = 5;
            var retryDelay = TimeSpan.FromSeconds(1);
            var tcpPoints = new IPoint[count];

            for (int i = 0; i < count; i++)
            {
                var daemonAddresses = _addressResolver.Resolve(daemonHostUrl);
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        var tcpClient = new TcpClient();
                        await tcpClient.ConnectAsync(daemonAddresses, daemonPort);

                        var networkChannel = new NetworkChannel(tcpClient);
                        networkChannel.SetCancellation(cancellationToken);
                        var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                        tcpPoints[i] = new Point(jobId, moduleId, networkChannel, argumentsProvider);

                        _logger.LogInformation("Point {Index}/{Count} created successfully for job {JobId}", i + 1, count, jobId);
                        break;
                    }
                    catch (Exception ex) when (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning(ex, "Failed to connect to daemon {Daemon} on attempt {Attempt}, retrying...", daemonHostUrl, attempt + 1);
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                }
            }

            return tcpPoints;
        }

        private async Task<IPoint[]> CreatePointsViaServiceBusAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            await using var serviceBusClient = new ServiceBusClient(_serviceBusConfiguration.ConnectionString);
            await using var sender = serviceBusClient.CreateSender(_serviceBusConfiguration.QueueName);

            // Resolve the host address once — all daemons will connect back to this address.
            var hostAddress = Dns.GetHostName();
            var hostAddresses = Dns.GetHostAddresses(hostAddress);
            var hostUrl = hostAddresses.FirstOrDefault(a => !IPAddress.IsLoopback(a))?.ToString() ?? hostAddress;

            // Phase 1 — register all pending connection slots and publish all messages before
            // awaiting any TCP connections. The TCS must be registered before publishing to avoid
            // a race where a very fast daemon connects back before we have registered the slot.
            var connectionTasks = new Task<NetworkChannel>[count];
            for (int i = 0; i < count; i++)
            {
                var correlationId = Guid.NewGuid().ToString();

                connectionTasks[i] = _hostTcpServer.WaitForConnectionAsync(correlationId, cancellationToken);

                var request = new PointCreationRequest
                {
                    JobId = jobId,
                    ModuleId = moduleId,
                    Arguments = arguments,
                    HostUrl = hostUrl,
                    HostPort = _hostTcpConfiguration.Port,
                    CorrelationId = correlationId
                };

                await sender.SendMessageAsync(
                    new ServiceBusMessage(JsonSerializer.Serialize(request)) { MessageId = correlationId },
                    cancellationToken);

                _logger.LogInformation(
                    "Published point request {Index}/{Count} for job {JobId} (correlationId={CorrelationId})",
                    i + 1, count, jobId, correlationId);
            }

            _logger.LogInformation("All {Count} point requests published for job {JobId}; awaiting daemon connections", count, jobId);

            // Phase 2 — await all daemon connections concurrently.
            var channels = await Task.WhenAll(connectionTasks);

            _logger.LogInformation("All {Count} daemons connected for job {JobId}", count, jobId);

            // Phase 3 — build Point instances from the resolved NetworkChannels.
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
