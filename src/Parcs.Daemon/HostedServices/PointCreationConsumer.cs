using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parcs.Core.Messaging;
using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Daemon.HostedServices
{
    /// <summary>
    /// Takes one point-creation request from the point queue (Pub/Sub, Service Bus or SQS),
    /// connects back to the point's parent (the Host or another daemon), and orchestrates this
    /// daemon's work for that point.
    ///
    /// The pod processes exactly one request and then stops
    /// (<see cref="IHostApplicationLifetime.StopApplication"/>), matching KEDA's ScaledJob model
    /// where a new pod is created per message. A failed request is returned to the queue for
    /// redelivery; the ScaledJob backoffLimit caps retries at the pod level.
    /// </summary>
    public sealed class PointCreationConsumer(
        IPointRequestReceiver pointRequestReceiver,
        ICurrentPointRequestAccessor currentPointRequestAccessor,
        IChannelOrchestrator channelOrchestrator,
        ILogger<PointCreationConsumer> logger,
        IHostApplicationLifetime applicationLifetime) : IHostedService
    {
        private readonly IPointRequestReceiver _pointRequestReceiver = pointRequestReceiver;
        private readonly ICurrentPointRequestAccessor _currentPointRequestAccessor = currentPointRequestAccessor;
        private readonly IChannelOrchestrator _channelOrchestrator = channelOrchestrator;
        private readonly ILogger<PointCreationConsumer> _logger = logger;
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

        private CancellationTokenSource _cts;
        private Task _consumeTask;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_pointRequestReceiver.IsEnabled)
            {
                _logger.LogInformation("No point queue configured; point creation consumer will not start.");
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        private async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _pointRequestReceiver.ReceiveOneAsync(HandleAsync, cancellationToken);
                _logger.LogInformation("Point completed, exiting daemon");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing point creation request: {Message}", ex.Message);
            }

            _applicationLifetime.StopApplication();
        }

        private async Task HandleAsync(PointCreationRequest request, CancellationToken cancellationToken)
        {
            _currentPointRequestAccessor.Current = request;

            if (request.RequestedAt is { } requestedAt)
            {
                // Queue-to-start latency: broker → KEDA polling → pod scheduling (→ node provisioning).
                _logger.LogInformation(
                    "Point request for job {JobId} picked up {ProvisioningSeconds:F2}s after it was published",
                    request.JobId, (DateTimeOffset.UtcNow - requestedAt).TotalSeconds);
            }

            _logger.LogInformation(
                "Received point creation request for job {JobId}, connecting to parent {HostUrl}:{Port}",
                request.JobId, request.HostUrl, request.HostPort);

            var hostAddresses = Dns.GetHostAddresses(request.HostUrl);
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(hostAddresses, request.HostPort, cancellationToken);

            var networkChannel = new NetworkChannel(tcpClient);

            // The correlationId handshake lets the parent match this TCP connection to the exact
            // point request it published.
            await networkChannel.WriteDataAsync(request.CorrelationId);

            _logger.LogInformation("Handshake sent, starting TCP communication for job {JobId}", request.JobId);

            await _channelOrchestrator.OrchestrateAsync(networkChannel, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_cts is null)
            {
                return;
            }

            await _cts.CancelAsync();

            if (_consumeTask is not null)
            {
                await Task.WhenAny(_consumeTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }
    }
}
