using Parcs.Core;
using Parcs.TCP.Host.EntryPoint;

namespace Parcs.TCP.Host.Models
{
    internal class HostInfo : IHostInfo
    {
        private readonly HostServer _server;

        public HostInfo(HostServer server)
        {
            _server = server;
        }

        public IPoint[] GetConnectedDaemons() => _server.ActiveSessions.Select(s => new Point(s.Value)).ToArray();
    }
}