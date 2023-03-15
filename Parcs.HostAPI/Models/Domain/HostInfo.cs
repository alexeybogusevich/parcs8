using Parcs.Core;
using Parcs.TCP.Host.Models;
using System.Net;
using System.Net.Sockets;

namespace Parcs.HostAPI.Models.Domain
{
    internal sealed class HostInfo : IHostInfo
    {
        private readonly Queue<Daemon> _unusedDaemons;
        private readonly int _initialDaemonsNumber;

        public HostInfo(IEnumerable<Daemon> daemons)
        {
            _unusedDaemons = new Queue<Daemon>(daemons);
            _initialDaemonsNumber = daemons.Count();
        }

        public int MaximumPointsNumber => _initialDaemonsNumber;

        public async Task<IPoint> CreatePointAsync()
        {
            if (!_unusedDaemons.TryDequeue(out var configurationToUse))
            {
                throw new ArgumentException("No more points can be created.");
            }

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(configurationToUse.HostUrl, configurationToUse.Port);

            return new Point(tcpClient);
        }
    }
}