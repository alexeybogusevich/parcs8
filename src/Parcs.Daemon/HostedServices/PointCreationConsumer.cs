using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;

namespace Parcs.Daemon.HostedServices
{
    public sealed class PointCreationConsumer(
        IOptions<ServiceBusConfiguration> serviceBusOptions,
        IChannelOrchestrator channelOrchestrator,
        ILogger<PointCreationConsumer> logger,
        IHostApplicationLifetime applicationLifetime) : IHostedService
    {
        private readonly ServiceBusConfiguration _serviceBusConfiguration = serviceBusOptions.Value;
        private readonly IChannelOrchestrator _channelOrchestrator = channelOrchestrator;
        private readonly ILogger<PointCreationConsumer> _logger = logger;
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
        private ServiceBusProcessor _processor;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_serviceBusConfiguration.ConnectionString) || string.IsNullOrEmpty(_serviceBusConfiguration.QueueName))
            {
                _logger.LogWarning("Service Bus configuration is missing. Point creation consumer will not start.");
                return Task.CompletedTask;
            }

            _ = Task.Run(async () => await ProcessMessagesAsync(cancellationToken), cancellationToken);

            return Task.CompletedTask;
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            await using var serviceBusClient = new ServiceBusClient(_serviceBusConfiguration.ConnectionString);
            _processor = serviceBusClient.CreateProcessor(_serviceBusConfiguration.QueueName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1
            });

            _processor.ProcessMessageAsync += MessageHandler;
            _processor.ProcessErrorAsync += ErrorHandler;

            _logger.LogInformation("Starting Service Bus processor for queue {QueueName}", _serviceBusConfiguration.QueueName);

            await _processor.StartProcessingAsync(cancellationToken);

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            try
            {
                var messageBody = args.Message.Body.ToString();
                var request = JsonSerializer.Deserialize<PointCreationRequest>(messageBody);

                if (request == null)
                {
                    _logger.LogError("Failed to deserialize point creation request");
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                _logger.LogInformation("Received point creation request for job {JobId}, connecting to host {HostUrl}:{Port}", request.JobId, request.HostUrl, request.HostPort);

                var hostAddresses = Dns.GetHostAddresses(request.HostUrl);
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(hostAddresses, request.HostPort);

                _logger.LogInformation("Connected to host, starting TCP communication");

                var networkChannel = new NetworkChannel(tcpClient);

                await _channelOrchestrator.OrchestrateAsync(networkChannel, args.CancellationToken);

                _logger.LogInformation("TCP communication completed, exiting daemon");

                await args.CompleteMessageAsync(args.Message);

                _applicationLifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing point creation message: {Message}", ex.Message);
                await args.AbandonMessageAsync(args.Message);
                _applicationLifetime.StopApplication();
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus error: {ErrorSource}", args.ErrorSource);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_processor != null)
            {
                _logger.LogInformation("Stopping Service Bus processor");
                await _processor.StopProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
            }
        }
    }
}
