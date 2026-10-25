using Microsoft.Extensions.Logging;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Net;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Parcs.Core.Services
{
    /// <inheritdoc/>
    public sealed class PeerChannelService(CallbackTcpServer callbackTcpServer, ILogger<PeerChannelService> logger) : IPeerChannelService
    {
        private readonly CallbackTcpServer _callbackTcpServer = callbackTcpServer;
        private readonly ILogger<PeerChannelService> _logger = logger;

        public IPeerEndpoint OpenEndpoint(CancellationToken cancellationToken)
        {
            // The listener is registered before the address is handed out, so a fast peer cannot
            // connect before it exists.
            var correlationId = Guid.NewGuid().ToString("N");
            var listenerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var connections = _callbackTcpServer.Listen(correlationId, listenerCancellation.Token);
            var (address, port) = _callbackTcpServer.GetAdvertisedEndpoint();

            return new PeerEndpoint(PeerAddress.Format(address, port, correlationId), connections, listenerCancellation, cancellationToken);
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
            string address, ChannelReader<NetworkChannel> connections, CancellationTokenSource listenerCancellation, CancellationToken channelCancellation) : IPeerEndpoint
        {
            private int _disposed;

            public string Address { get; } = address;

            public async Task<IChannel> AcceptAsync(CancellationToken cancellationToken = default)
            {
                NetworkChannel channel;
                try
                {
                    channel = await connections.ReadAsync(cancellationToken);
                }
                catch (ChannelClosedException)
                {
                    throw new OperationCanceledException("The endpoint is closed.");
                }

                channel.SetCancellation(channelCancellation);
                return channel;
            }

            public ValueTask DisposeAsync()
            {
                // Stops listening and closes connections nobody accepted; accepted channels belong to the caller.
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return ValueTask.CompletedTask;
                }

                listenerCancellation.Cancel();
                listenerCancellation.Dispose();
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
