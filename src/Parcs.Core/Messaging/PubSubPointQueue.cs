using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Parcs.Core.Messaging
{
    /// <summary>
    /// Google Cloud Pub/Sub transport (GKE, KEDA "gcp-pubsub" scaler). Authentication comes from
    /// Application Default Credentials — Workload Identity on GKE.
    /// </summary>
    public sealed class PubSubPointRequestPublisher(
        IOptions<PubSubConfiguration> options,
        ILogger<PubSubPointRequestPublisher> logger) : IPointRequestPublisher, IAsyncDisposable
    {
        private readonly PubSubConfiguration _configuration = options.Value;
        private readonly ILogger<PubSubPointRequestPublisher> _logger = logger;

        // PublisherClient owns gRPC channels and a batching buffer; one per topic for the process lifetime.
        private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _publishers = new();

        public bool IsEnabled => true;

        public async Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default)
        {
            var topicId = requiresGpu ? _configuration.GpuTopicId : _configuration.TopicId;

            if (string.IsNullOrEmpty(topicId))
            {
                throw new InvalidOperationException($"Pub/Sub topic for {(requiresGpu ? "GPU" : "CPU")} points is not configured.");
            }

            var publisher = await _publishers
                .GetOrAdd(topicId, id => new Lazy<Task<PublisherClient>>(
                    () => PublisherClient.CreateAsync(TopicName.FromProjectTopic(_configuration.ProjectId, id))))
                .Value;

            var publishTasks = requests.Select(request => publisher.PublishAsync(new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(request)),
                Attributes =
                {
                    ["correlationId"] = request.CorrelationId,
                    ["jobId"] = request.JobId.ToString(),
                }
            }));

            var messageIds = await Task.WhenAll(publishTasks);

            _logger.LogInformation(
                "Published {Count} point requests to Pub/Sub topic {TopicId}: {MessageIds}",
                requests.Count, topicId, string.Join(", ", messageIds));
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var lazy in _publishers.Values.Where(l => l.IsValueCreated))
            {
                var publisher = await lazy.Value;
                await publisher.ShutdownAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    public sealed class PubSubPointRequestReceiver(
        IOptions<PubSubConfiguration> options,
        ILogger<PubSubPointRequestReceiver> logger) : IPointRequestReceiver
    {
        private readonly PubSubConfiguration _configuration = options.Value;
        private readonly ILogger<PubSubPointRequestReceiver> _logger = logger;

        public bool IsEnabled => true;

        public async Task ReceiveOneAsync(Func<PointCreationRequest, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        {
            // The client's defaults deliver several messages concurrently (one pull stream per
            // processor, several outstanding messages each), which would let this single-point pod
            // grab a second request and orphan it. Pin to one stream and one outstanding message.
            var subscriber = await new SubscriberClientBuilder
            {
                SubscriptionName = SubscriptionName.FromProjectSubscription(_configuration.ProjectId, _configuration.SubscriptionId),
                ClientCount = 1,
                Settings = new SubscriberClient.Settings
                {
                    FlowControlSettings = new FlowControlSettings(maxOutstandingElementCount: 1, maxOutstandingByteCount: null),
                },
            }.BuildAsync(cancellationToken);

            var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => processed.TrySetCanceled(cancellationToken));

            _logger.LogInformation(
                "Waiting for a point request on Pub/Sub subscription {SubscriptionId}", _configuration.SubscriptionId);

            // The subscriber extends the ack deadline automatically while the handler runs.
            var subscriberTask = subscriber.StartAsync(async (message, ct) =>
            {
                if (processed.Task.IsCompleted)
                {
                    return SubscriberClient.Reply.Nack;
                }

                PointCreationRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<PointCreationRequest>(message.Data.ToStringUtf8());
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Discarding malformed point request {MessageId}", message.MessageId);
                    return SubscriberClient.Reply.Ack;
                }

                if (request is null)
                {
                    _logger.LogError("Discarding empty point request {MessageId}", message.MessageId);
                    return SubscriberClient.Reply.Ack;
                }

                try
                {
                    await handler(request, cancellationToken);
                    processed.TrySetResult();
                    return SubscriberClient.Reply.Ack;
                }
                catch (Exception ex)
                {
                    processed.TrySetException(ex);
                    return SubscriberClient.Reply.Nack;
                }
            });

            try
            {
                await processed.Task;
            }
            finally
            {
                // Lets the Ack/Nack reply above reach the server before the stream closes.
                await subscriber.StopAsync(TimeSpan.FromSeconds(10));
                await subscriberTask;
            }
        }
    }
}
