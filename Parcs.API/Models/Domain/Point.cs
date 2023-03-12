using Parcs.Core;
using Parcs.HostAPI.Clients.TCP;
using System.Collections.Concurrent;

namespace Parcs.TCP.Host.Models
{
    internal sealed class Point : IPoint
    {
        private static readonly ConcurrentDictionary<string, DaemonClient> _connectedClients = new();

        private readonly DaemonClient _daemonClient;

        public Point(string ipAddress, int port)
        {
            if (_connectedClients.TryGetValue(ipAddress, out var existingClient))
            {
                _daemonClient = existingClient;
                return;
            }

            _daemonClient = new DaemonClient(ipAddress, port);
            _connectedClients.TryAdd(ipAddress, _daemonClient);
        }

        public IChannel CreateChannel()
        {
            if (!_daemonClient.IsConnected && !_daemonClient.Connect())
            {
                throw new ArgumentException($"Can't connect to the daemon ({_daemonClient.Endpoint})");
            }

            var channel = new Channel(_daemonClient);

            if (channel.ReadSignal() != Signal.AcknowledgeConnection)
            {
                throw new IOException("The connection was not acknowledged by the daemon.");
            }

            return channel;
        }

        public void Delete()
        {
            _daemonClient.DisconnectAndStop();
        }
    }
}