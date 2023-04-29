using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Daemon.Configuration;
using System.Net.Sockets;
using System.Net;
using Parcs.Core.Models;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.HostedServices
{
    public class TcpServer : IHostedService
    {
        private readonly TcpListener _tcpListener;
        private readonly ISignalHandlerFactory _signalHandlerFactory;
        private readonly ILogger<TcpServer> _logger;

        public TcpServer(ISignalHandlerFactory signalHandlerFactory, IOptions<NodeConfiguration> nodeOptions, ILogger<TcpServer> logger)
        {
            _tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, nodeOptions.Value.Port));
            _signalHandlerFactory = signalHandlerFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Server starting...");

            _tcpListener.Start();
            _logger.LogInformation("Done!");

            while (true)
            {
                _logger.LogInformation("Waiting for a connection...");

                using var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                using var channel = new NetworkChannel(tcpClient.GetStream());

                await HandleConnectionAsync(channel, cancellationToken);
            }
        }

        private async Task HandleConnectionAsync(NetworkChannel channel, CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    var signal = await channel.ReadSignalAsync();

                    if (signal == Signal.CloseConnection)
                    {
                        return;
                    }

                    var signalHandler = _signalHandlerFactory.Create(signal);

                    await signalHandler.HandleAsync(channel, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception thrown: {Message}.", ex.Message);

                    return;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping the server...");

            _tcpListener.Stop();
            _logger.LogInformation("Done!");

            return Task.CompletedTask;
        }
    }
}