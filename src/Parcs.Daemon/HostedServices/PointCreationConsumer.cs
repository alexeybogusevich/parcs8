using Google.Cloud.PubSub.V1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Parcs.Daemon.HostedServices
{
    /// <summary>
    /// Pulls one point-creation request from Google Cloud Pub/Sub, establishes a TCP connection
    /// back to the Host, and orchestrates the daemon's work for that job.
    ///
    /// This is the GCP equivalent of the former Azure Service Bus consumer.
    ///
    /// Pub/Sub guarantees at-least-once delivery:
    ///   • ACK  → message removed from the subscription (job processed or permanently invalid).
    ///   • NACK → message returned to the subscription for redelivery (transient error).
    ///
    /// The daemon pod is designed to process exactly one message and then stop
    /// (<see cref="IHostApplicationLifetime.StopApplication"/>), matching KEDA's
    /// ScaledJob model where a new pod is created per message.
    /// </summary>
    public sealed class PointCreationConsumer(
        IOptions<PubSubConfiguration> pubSubOptions,
        IChannelOrchestrator channelOrchestrator,
        ILogger<PointCreationConsumer> logger,
        IHostApplicationLifetime applicationLifetime) : IHostedService
    {
        private readonly PubSubConfiguration _pubSubConfiguration = pubSubOptions.Value;
        private readonly IChannelOrchestrator _channelOrchestrator = channelOrchestrator;
        private readonly ILogger<PointCreationConsumer> _logger = logger;
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

        private SubscriberClient _subscriber;
        private CancellationTokenSource _cts;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_pubSubConfiguration.ProjectId) ||
                string.IsNullOrEmpty(_pubSubConfiguration.SubscriptionId))
            {
                _logger.LogWarning(
                    "Pub/Sub configuration is missing (ProjectId or SubscriptionId). " +
                    "Point creation consumer will not start.");
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Fire-and-forget: SubscriberClient.StartAsync blocks until stopped.
            _ = Task.Run(() => StartSubscriberAsync(_cts.Token), _cts.Token);

            return Task.CompletedTask;
        }

        private async Task StartSubscriberAsync(CancellationToken cancellationToken)
        {
            var subscriptionName = SubscriptionName.FromProjectSubscription(
                _pubSubConfiguration.ProjectId,
                _pubSubConfiguration.SubscriptionId);

            _subscriber = await SubscriberClient.CreateAsync(subscriptionName);

            _logger.LogInformation(
                "Starting Pub/Sub subscriber for subscription {SubscriptionId} in project {ProjectId}",
                _pubSubConfiguration.SubscriptionId, _pubSubConfiguration.ProjectId);

            // StartAsync runs the message loop until StopAsync is called.
            // The handler is invoked once per delivered message.
            await _subscriber.StartAsync(async (message, ct) =>
            {
                try
                {
                    var messageBody = message.Data.ToStringUtf8();
                    var request = JsonSerializer.Deserialize<PointCreationRequest>(messageBody);

                    if (request == null)
                    {
                        _logger.LogError("Failed to deserialize point creation request — ACKing to discard.");
                        return SubscriberClient.Reply.Ack;
                    }

                    _logger.LogInformation(
                        "Received point creation request for job {JobId}, connecting to host {HostUrl}:{Port}",
                        request.JobId, request.HostUrl, request.HostPort);

                    var hostAddresses = Dns.GetHostAddresses(request.HostUrl);
                    var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(hostAddresses, request.HostPort);

                    _logger.LogInformation(
                        "Connected to host, sending correlationId handshake for job {JobId}", request.JobId);

                    var networkChannel = new NetworkChannel(tcpClient);

                    // The correlationId handshake lets the Host match this TCP connection
                    // to the exact point-creation request that published the Pub/Sub message.
                    await networkChannel.WriteDataAsync(request.CorrelationId);

                    _logger.LogInformation(
                        "Handshake sent, starting TCP communication for job {JobId}", request.JobId);

                    await _channelOrchestrator.OrchestrateAsync(networkChannel, ct);

                    _logger.LogInformation("TCP communication completed, exiting daemon");

                    // ACK before stopping so the message is not redelivered if stop is slow.
                    _applicationLifetime.StopApplication();
                    return SubscriberClient.Reply.Ack;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing point creation message: {Message}", ex.Message);
                    _applicationLifetime.StopApplication();

                    // NACK: Pub/Sub will redeliver after the acknowledgement deadline.
                    // The KEDA ScaledJob backoffLimit provides a cap on retries at the pod level.
                    return SubscriberClient.Reply.Nack;
                }
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Pub/Sub subscriber");
            _cts?.Cancel();

            if (_subscriber is not null)
            {
                await _subscriber.StopAsync(cancellationToken);
            }
        }
    }
}
