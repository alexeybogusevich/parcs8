using Microsoft.Extensions.Logging;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Net;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Core.Services
{
    /// <inheritdoc/>
    public sealed class PeerChannelService(CallbackTcpServer callbackTcpServer, ILogger<PeerChannelService> logger) : IPeerChannelService
    {
        private readonly CallbackTcpServer _callbackTcpServer = callbackTcpServer;
        private readonly ILogger<PeerChannelService> _logger = logger;

        public IPeerEndpoint OpenEndpoint(CancellationToken cancellationToken)
        {
            // The slot is registered before the address is handed out, so a fast peer cannot
            // connect before it exists.
            var correlationId = Guid.NewGuid().ToString("N");
            var slotCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var connection = _callbackTcpServer.WaitForConnectionAsync(correlationId, slotCancellation.Token);
            var (address, port) = _callbackTcpServer.GetAdvertisedEndpoint();

            return new PeerEndpoint(PeerAddress.Format(address, port, correlationId), connection, slotCancellation, cancellationToken);
        }

        public async Task<IChannel> ConnectAsync(string endpointAddress, CancellationToken cancellationToken)
        {
            var (host, port, correlationId) = PeerAddress.Parse(endpointAddress);
            var addresses = IPAddress.TryParse(host, out var ip) ? [ip] : await Dns.GetHostAddressesAsync(host, cancellationToken);

            var tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(addresses, port, cancellationToken);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

            var channel = new NetworkChannel(tcpClient);
            channel.SetCancellation(cancellationToken);
            // The callback server matches the connection to the endpoint by its first message.
            await channel.WriteDataAsync(correlationId);

            _logger.LogDebug("Connected to peer endpoint {Address}", endpointAddress);
            return channel;
        }

        private sealed class PeerEndpoint(
            string address, Task<NetworkChannel> connection, CancellationTokenSource slotCancellation, CancellationToken channelCancellation) : IPeerEndpoint
        {
            private int _accepted;
            private int _disposed;

            public string Address { get; } = address;

            public async Task<IChannel> AcceptAsync(CancellationToken cancellationToken = default)
            {
                if (Interlocked.Exchange(ref _accepted, 1) == 1)
                {
                    throw new InvalidOperationException("An endpoint accepts one channel only; open another endpoint for the next peer.");
                }

                var channel = await connection.WaitAsync(cancellationToken);
                channel.SetCancellation(channelCancellation);
                return channel;
            }

            public ValueTask DisposeAsync()
            {
                // Frees the slot if nobody connected; an accepted channel belongs to the caller.
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return ValueTask.CompletedTask;
                }

                slotCancellation.Cancel();
                slotCancellation.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>Endpoint address format: <c>host:port/correlationId</c>.</summary>
    public static class PeerAddress
    {
        public static string Format(string host, int port, string correlationId) => $"{host}:{port}/{correlationId}";

        public static (string Host, int Port, string CorrelationId) Parse(string address)
        {
            var slash = address?.LastIndexOf('/') ?? -1;
            var colon = slash > 0 ? address.LastIndexOf(':', slash - 1) : -1;

            if (colon <= 0 || slash == address.Length - 1 ||
                !int.TryParse(address.AsSpan(colon + 1, slash - colon - 1), out var port))
            {
                throw new FormatException($"'{address}' is not a peer endpoint address (host:port/id).");
            }

            return (address[..colon], port, address[(slash + 1)..]);
        }
    }
}
