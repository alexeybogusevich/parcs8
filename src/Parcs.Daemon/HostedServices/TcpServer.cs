using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Daemon.Configuration;
using System.Net.Sockets;
using System.Net;
using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.HostedServices
{
    public class TcpServer(
        IChannelOrchestrator channelOrchestrator,
        IOptions<DaemonConfiguration> nodeOptions,
        ILogger<TcpServer> logger) : IHostedService
    {
        private readonly TcpListener _tcpListener = new(new IPEndPoint(IPAddress.Any, nodeOptions.Value.Port));
        private readonly IChannelOrchestrator _channelOrchestrator = channelOrchestrator;
        private readonly ILogger<TcpServer> _logger = logger;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => await StartServerAsync(cancellationToken), cancellationToken);

            return Task.CompletedTask;
        }

        private async Task StartServerAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("TCP Server starting...");

            _tcpListener.Start();
            _logger.LogInformation("Done!");

            while (true)
            {
                _logger.LogInformation("Waiting for a connection...");

                var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                var networkChannel = new NetworkChannel(tcpClient);

                _ = Task.Run(async () => await _channelOrchestrator.OrchestrateAsync(networkChannel, cancellationToken), cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping the TCP server...");

            _tcpListener.Stop();
            _logger.LogInformation("Done!");

            return Task.CompletedTask;
        }
    }
}