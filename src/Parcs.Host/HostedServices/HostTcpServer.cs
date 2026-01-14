using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;

namespace Parcs.Host.HostedServices
{
    public sealed class HostTcpServer(
        IOptions<HostTcpConfiguration> hostTcpOptions,
        ILogger<HostTcpServer> logger) : IHostedService
    {
        private readonly HostTcpConfiguration _hostTcpConfiguration = hostTcpOptions.Value;
        private readonly ILogger<HostTcpServer> _logger = logger;
        private TcpListener _tcpListener;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TcpClient>> _pendingConnections = new();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _tcpListener = new TcpListener(IPAddress.Any, _hostTcpConfiguration.Port);
            _tcpListener.Start();

            _logger.LogInformation("Host TCP server started on port {Port}", _hostTcpConfiguration.Port);

            _ = Task.Run(async () => await AcceptConnectionsAsync(cancellationToken), cancellationToken);

            return Task.CompletedTask;
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);

                    _logger.LogInformation("Accepted daemon connection");

                    if (_pendingConnections.Count > 0)
                    {
                        var firstEntry = _pendingConnections.First();
                        firstEntry.Value.SetResult(tcpClient);
                        _pendingConnections.TryRemove(firstEntry.Key, out _);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting TCP connection");
                }
            }
        }

        public Task<TcpClient> WaitForConnectionAsync(string correlationId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TcpClient>();
            _pendingConnections.TryAdd(correlationId, tcs);

            cancellationToken.Register(() =>
            {
                if (_pendingConnections.TryRemove(correlationId, out var removedTcs))
                {
                    removedTcs.TrySetCanceled();
                }
            });

            return tcs.Task;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Host TCP server");

            _tcpListener?.Stop();

            foreach (var tcs in _pendingConnections.Values)
            {
                tcs.TrySetCanceled();
            }
            _pendingConnections.Clear();

            return Task.CompletedTask;
        }
    }
}
