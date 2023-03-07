using Parcs.Core;
using Parcs.TCP.Host.EntryPoint;

namespace Parcs.TCP.Host.Models
{
    internal class Point : IPoint
    {
        private readonly DaemonClient _daemonClient;

        public Point(string ipAddress, int port)
        {
            _daemonClient = new DaemonClient(ipAddress, port);
        }

        public IChannel CreateChannel()
        {
            _daemonClient.Connect();
            return new Channel(_daemonClient);
        }

        public void Delete() => _daemonClient.DisconnectAndStop();
    }
}