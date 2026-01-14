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
            var daemonAddresses = _addressResolver.Resolve(daemonHostUrl);

            if (daemonAddresses.Any(IPAddress.IsLoopback))
            {
                var internalChannelId = _internalChannelManager.Create();
                _ = _internalChannelManager.TryGet(internalChannelId, out var internalChannelPair);
                var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                return new Point(jobId, moduleId, internalChannelPair.Item1, argumentsProvider);
            }

            if (!string.IsNullOrEmpty(_serviceBusConfiguration.ConnectionString) && !string.IsNullOrEmpty(_serviceBusConfiguration.QueueName))
            {
                await using var serviceBusClient = new ServiceBusClient(_serviceBusConfiguration.ConnectionString);
                await using var sender = serviceBusClient.CreateSender(_serviceBusConfiguration.QueueName);

                var hostAddress = Dns.GetHostName();
                var hostAddresses = Dns.GetHostAddresses(hostAddress);
                var hostUrl = hostAddresses.FirstOrDefault(a => !IPAddress.IsLoopback(a))?.ToString() ?? hostAddress;

                var correlationId = Guid.NewGuid().ToString();
                var request = new PointCreationRequest
                {
                    JobId = jobId,
                    ModuleId = moduleId,
                    Arguments = arguments,
                    HostUrl = hostUrl,
                    HostPort = _hostTcpConfiguration.Port,
                    CorrelationId = correlationId
                };

                var messageBody = JsonSerializer.Serialize(request);
                var message = new ServiceBusMessage(messageBody)
                {
                    MessageId = correlationId
                };

                _logger.LogInformation("Publishing point creation request for job {JobId}", jobId);

                await sender.SendMessageAsync(message, cancellationToken);

                _logger.LogInformation("Waiting for daemon connection on port {Port}", _hostTcpConfiguration.Port);

                var tcpClient = await _hostTcpServer.WaitForConnectionAsync(correlationId, cancellationToken);

                var networkChannel = new NetworkChannel(tcpClient);
                networkChannel.SetCancellation(cancellationToken);
                var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                var point = new Point(jobId, moduleId, networkChannel, argumentsProvider);

                _logger.LogInformation("Point created successfully for job {JobId}", jobId);

                return point;
            }

            var maxRetries = 5;
            var retryDelay = TimeSpan.FromSeconds(1);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(daemonAddresses, daemonPort);

                    var networkChannel = new NetworkChannel(tcpClient);
                    networkChannel.SetCancellation(cancellationToken);
                    var argumentsProvider = _argumentsProviderFactory.Create(arguments);
                    var point = new Point(jobId, moduleId, networkChannel, argumentsProvider);

                    _logger.LogInformation("Point created successfully for job {JobId}", jobId);

                    return point;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning(ex, "Failed to connect to daemon {Daemon} on attempt {Attempt}, retrying...", daemonHostUrl, attempt + 1);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Failed to create point after {maxRetries} attempts");
        }
    }
}
