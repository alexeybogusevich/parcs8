using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using System.Text.Json;

namespace Parcs.Core.Messaging
{
    internal static class SqsClientFactory
    {
        public static AmazonSQSClient Create(SqsConfiguration configuration)
        {
            var config = new AmazonSQSConfig();

            if (!string.IsNullOrEmpty(configuration.ServiceUrl))
            {
                config.ServiceURL = configuration.ServiceUrl;
                config.AuthenticationRegion = configuration.Region;
            }
            else if (!string.IsNullOrEmpty(configuration.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(configuration.Region);
            }

            // Default credential chain: IRSA / EKS Pod Identity in-cluster, profile or env locally.
            return new AmazonSQSClient(config);
        }
    }

    /// <summary>
    /// Amazon SQS transport (EKS, KEDA "aws-sqs-queue" scaler).
    /// </summary>
    public sealed class SqsPointRequestPublisher(
        IOptions<SqsConfiguration> options,
        ILogger<SqsPointRequestPublisher> logger) : IPointRequestPublisher, IDisposable
    {
        // SendMessageBatch accepts at most 10 entries per call.
        private const int MaxBatchSize = 10;

        private readonly SqsConfiguration _configuration = options.Value;
        private readonly ILogger<SqsPointRequestPublisher> _logger = logger;
        private readonly Lazy<AmazonSQSClient> _client = new(() => SqsClientFactory.Create(options.Value));

        public bool IsEnabled => true;

        public async Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default)
        {
            var queueUrl = requiresGpu ? _configuration.GpuQueueUrl : _configuration.QueueUrl;

            if (string.IsNullOrEmpty(queueUrl))
            {
                throw new InvalidOperationException($"SQS queue for {(requiresGpu ? "GPU" : "CPU")} points is not configured.");
            }

            foreach (var chunk in requests.Chunk(MaxBatchSize))
            {
                var response = await _client.Value.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = queueUrl,
                    Entries = chunk.Select((request, index) => new SendMessageBatchRequestEntry
                    {
                        Id = index.ToString(),
                        MessageBody = JsonSerializer.Serialize(request),
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            ["correlationId"] = new() { DataType = "String", StringValue = request.CorrelationId },
                            ["jobId"] = new() { DataType = "Number", StringValue = request.JobId.ToString() },
                        },
                    }).ToList(),
                }, cancellationToken);

                if (response.Failed is { Count: > 0 })
                {
                    var failures = string.Join("; ", response.Failed.Select(f => $"{f.Code}: {f.Message}"));
                    throw new InvalidOperationException($"Failed to publish {response.Failed.Count} point requests to SQS: {failures}");
                }
            }

            _logger.LogInformation("Published {Count} point requests to SQS queue {QueueUrl}", requests.Count, queueUrl);
        }

        public void Dispose()
        {
            if (_client.IsValueCreated)
            {
                _client.Value.Dispose();
            }
        }
    }

    public sealed class SqsPointRequestReceiver(
        IOptions<SqsConfiguration> options,
        ILogger<SqsPointRequestReceiver> logger) : IPointRequestReceiver
    {
        private readonly SqsConfiguration _configuration = options.Value;
        private readonly ILogger<SqsPointRequestReceiver> _logger = logger;

        public bool IsEnabled => true;

        public async Task ReceiveOneAsync(Func<PointCreationRequest, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        {
            using var client = SqsClientFactory.Create(_configuration);

            _logger.LogInformation("Waiting for a point request on SQS queue {QueueUrl}", _configuration.QueueUrl);

            while (true)
            {
                // Long polling, one message at a time: this pod hosts exactly one point.
                var response = await client.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _configuration.QueueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 20,
                    VisibilityTimeout = _configuration.VisibilityTimeoutSeconds,
                }, cancellationToken);

                var message = response.Messages?.FirstOrDefault();
                if (message is null)
                {
                    continue;
                }

                PointCreationRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<PointCreationRequest>(message.Body);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Discarding malformed point request {MessageId}", message.MessageId);
                    await client.DeleteMessageAsync(_configuration.QueueUrl, message.ReceiptHandle, cancellationToken);
                    continue;
                }

                if (request is null)
                {
                    _logger.LogError("Discarding empty point request {MessageId}", message.MessageId);
                    await client.DeleteMessageAsync(_configuration.QueueUrl, message.ReceiptHandle, cancellationToken);
                    continue;
                }

                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var heartbeat = ExtendVisibilityAsync(client, message, heartbeatCts.Token);

                try
                {
                    await handler(request, cancellationToken);
                }
                catch
                {
                    await StopHeartbeatAsync(heartbeatCts, heartbeat);

                    // Visibility 0 makes the message immediately available to another pod.
                    await client.ChangeMessageVisibilityAsync(_configuration.QueueUrl, message.ReceiptHandle, 0, CancellationToken.None);
                    throw;
                }

                await StopHeartbeatAsync(heartbeatCts, heartbeat);
                await client.DeleteMessageAsync(_configuration.QueueUrl, message.ReceiptHandle, CancellationToken.None);
                return;
            }
        }

        // Keeps the message invisible while the point runs, so SQS does not hand it to a second
        // pod (and KEDA does not count it as pending) in the middle of a long computation.
        private async Task ExtendVisibilityAsync(AmazonSQSClient client, Message message, CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(_configuration.VisibilityTimeoutSeconds / 2, 5));

            try
            {
                while (true)
                {
                    await Task.Delay(interval, cancellationToken);
                    await client.ChangeMessageVisibilityAsync(
                        _configuration.QueueUrl, message.ReceiptHandle, _configuration.VisibilityTimeoutSeconds, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extend visibility of point request {MessageId}", message.MessageId);
            }
        }

        private static async Task StopHeartbeatAsync(CancellationTokenSource heartbeatCts, Task heartbeat)
        {
            await heartbeatCts.CancelAsync();
            await heartbeat;
        }
    }
}
