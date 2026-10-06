using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Parcs.Core.Messaging
{
    /// <summary>
    /// Azure Service Bus transport (AKS, KEDA "azure-servicebus" scaler).
    /// </summary>
    public sealed class ServiceBusPointRequestPublisher(
        IOptions<ServiceBusConfiguration> options,
        ILogger<ServiceBusPointRequestPublisher> logger) : IPointRequestPublisher, IAsyncDisposable
    {
        private readonly ServiceBusConfiguration _configuration = options.Value;
        private readonly ILogger<ServiceBusPointRequestPublisher> _logger = logger;
        private readonly Lazy<ServiceBusClient> _client = new(() => new ServiceBusClient(options.Value.ConnectionString));
        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

        public bool IsEnabled => true;

        public async Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default)
        {
            var queueName = requiresGpu ? _configuration.GpuQueueName : _configuration.QueueName;

            if (string.IsNullOrEmpty(queueName))
            {
                throw new InvalidOperationException($"Service Bus queue for {(requiresGpu ? "GPU" : "CPU")} points is not configured.");
            }

            var sender = _senders.GetOrAdd(queueName, name => _client.Value.CreateSender(name));

            var messages = requests.Select(request =>
            {
                var message = new ServiceBusMessage(JsonSerializer.Serialize(request))
                {
                    ContentType = "application/json",
                    CorrelationId = request.CorrelationId,
                };
                message.ApplicationProperties["jobId"] = request.JobId;
                return message;
            });

            await sender.SendMessagesAsync(messages, cancellationToken);

            _logger.LogInformation("Published {Count} point requests to Service Bus queue {QueueName}", requests.Count, queueName);
        }

        public async ValueTask DisposeAsync()
        {
            if (_client.IsValueCreated)
            {
                await _client.Value.DisposeAsync();
            }
        }
    }

    public sealed class ServiceBusPointRequestReceiver(
        IOptions<ServiceBusConfiguration> options,
        ILogger<ServiceBusPointRequestReceiver> logger) : IPointRequestReceiver
    {
        private readonly ServiceBusConfiguration _configuration = options.Value;
        private readonly ILogger<ServiceBusPointRequestReceiver> _logger = logger;

        public bool IsEnabled => true;

        public async Task ReceiveOneAsync(Func<PointCreationRequest, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        {
            await using var client = new ServiceBusClient(_configuration.ConnectionString);

            // PrefetchCount = 0 so this single-point pod never holds a lock on a second message.
            await using var receiver = client.CreateReceiver(_configuration.QueueName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                PrefetchCount = 0,
            });

            _logger.LogInformation("Waiting for a point request on Service Bus queue {QueueName}", _configuration.QueueName);

            while (true)
            {
                var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
                if (message is null)
                {
                    continue;
                }

                PointCreationRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<PointCreationRequest>(message.Body.ToString());
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Discarding malformed point request {MessageId}", message.MessageId);
                    await receiver.CompleteMessageAsync(message, cancellationToken);
                    continue;
                }

                if (request is null)
                {
                    _logger.LogError("Discarding empty point request {MessageId}", message.MessageId);
                    await receiver.CompleteMessageAsync(message, cancellationToken);
                    continue;
                }

                using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var renewal = RenewLockAsync(receiver, message, renewalCts.Token);

                try
                {
                    await handler(request, cancellationToken);
                }
                catch
                {
                    await StopRenewalAsync(renewalCts, renewal);
                    await receiver.AbandonMessageAsync(message, cancellationToken: CancellationToken.None);
                    throw;
                }

                await StopRenewalAsync(renewalCts, renewal);
                await receiver.CompleteMessageAsync(message, CancellationToken.None);
                return;
            }
        }

        // Point lifetimes routinely exceed the queue's lock duration (≤ 5 min), so the lock is
        // renewed at half its remaining time; otherwise the message would be redelivered to a
        // second pod while this one is still computing.
        private async Task RenewLockAsync(ServiceBusReceiver receiver, ServiceBusReceivedMessage message, CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    var remaining = message.LockedUntil - DateTimeOffset.UtcNow;
                    var delay = TimeSpan.FromTicks(Math.Max(remaining.Ticks / 2, TimeSpan.FromSeconds(5).Ticks));
                    await Task.Delay(delay, cancellationToken);
                    await receiver.RenewMessageLockAsync(message, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to renew lock on point request {MessageId}", message.MessageId);
            }
        }

        private static async Task StopRenewalAsync(CancellationTokenSource renewalCts, Task renewal)
        {
            await renewalCts.CancelAsync();
            await renewal;
        }
    }
}
