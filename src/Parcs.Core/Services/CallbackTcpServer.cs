using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Core.Services
{
    /// <summary>
    /// Accepts connections from daemons that picked up a point request from the queue and dials
    /// back to the point's parent. Runs on the Host (top-level points) and on every daemon
    /// (nested points created by modules running there), which is what keeps the recursive
    /// control space working when points are provisioned through KEDA.
    /// </summary>
    public sealed class CallbackTcpServer(
        IOptions<HostTcpConfiguration> hostTcpOptions,
        ILogger<CallbackTcpServer> logger) : IHostedService
    {
        private readonly HostTcpConfiguration _hostTcpConfiguration = hostTcpOptions.Value;
        private readonly ILogger<CallbackTcpServer> _logger = logger;
        private TcpListener _tcpListener;

        // Cancelled in StopAsync to cleanly exit the AcceptConnectionsAsync loop.
        private CancellationTokenSource _cts = new();

        // Keyed by correlationId — each pending point request registers its own TCS.
        private readonly ConcurrentDictionary<string, TaskCompletionSource<NetworkChannel>> _pendingConnections = new();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();

            _tcpListener = new TcpListener(IPAddress.Any, _hostTcpConfiguration.Port);
            _tcpListener.Start();

            _logger.LogInformation("Callback TCP server started on port {Port}", _hostTcpConfiguration.Port);

            // Use _cts.Token (not the startup cancellationToken) so StopAsync can signal the loop to exit.
            _ = Task.Run(async () => await AcceptConnectionsAsync(_cts.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);

                    // Dispatch handshake to its own task so reading from one slow daemon
                    // does not delay accepting the next incoming connection.
                    _ = Task.Run(async () => await HandleConnectionAsync(tcpClient), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting TCP connection");
                }
            }
        }

        private async Task HandleConnectionAsync(TcpClient tcpClient)
        {
            try
            {
                var networkChannel = new NetworkChannel(tcpClient);

                // The daemon sends its correlationId as the very first message on the channel.
                // This lets us match the connection to the exact point request that triggered it,
                // which is essential for correctness when multiple points are created concurrently.
                var correlationId = await networkChannel.ReadStringAsync();

                _logger.LogInformation("Accepted daemon connection for correlationId {CorrelationId}", correlationId);

                if (_pendingConnections.TryRemove(correlationId, out var tcs))
                {
                    tcs.SetResult(networkChannel);
                }
                else
                {
                    _logger.LogWarning(
                        "No pending point request found for correlationId {CorrelationId}; closing connection",
                        correlationId);
                    networkChannel.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during correlationId handshake");
                tcpClient.Dispose();
            }
        }

        /// <summary>
        /// Registers a pending connection slot keyed by <paramref name="correlationId"/> and returns
        /// a Task that completes once the matching daemon connects and completes the handshake.
        /// </summary>
        public Task<NetworkChannel> WaitForConnectionAsync(string correlationId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<NetworkChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingConnections[correlationId] = tcs;

            cancellationToken.Register(() =>
            {
                if (_pendingConnections.TryRemove(correlationId, out var removedTcs))
                {
                    removedTcs.TrySetCanceled();
                }
            });

            return tcs.Task;
        }

        /// <summary>Address and port that child daemons should dial back to.</summary>
        public (string Address, int Port) GetAdvertisedEndpoint()
        {
            if (!string.IsNullOrEmpty(_hostTcpConfiguration.AdvertisedAddress))
            {
                return (_hostTcpConfiguration.AdvertisedAddress, _hostTcpConfiguration.Port);
            }

            var hostName = Dns.GetHostName();
            var address = Dns.GetHostAddresses(hostName)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString() ?? hostName;

            return (address, _hostTcpConfiguration.Port);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping callback TCP server");

            // Cancel first — causes AcceptTcpClientAsync(_cts.Token) to throw
            // OperationCanceledException, which the loop catches and breaks on cleanly.
            _cts.Cancel();
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
