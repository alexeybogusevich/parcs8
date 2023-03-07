using Parcs.Core;
using Parcs.TCP.Host.EntryPoint;

namespace Parcs.TCP.Host.Models
{
    internal sealed class Point : IPoint
    {
        private readonly DaemonClient _daemonClient;

        public Point(string ipAddress, int port)
        {
            _daemonClient = new DaemonClient(ipAddress, port);
        }

        public IChannel CreateChannel()
        {
            if (!_daemonClient.Connect())
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

        public void Delete() => _daemonClient.DisconnectAndStop();
    }
}