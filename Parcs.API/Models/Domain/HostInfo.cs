using Parcs.Core;
using Parcs.TCP.Host.Models;
using System.Net;
using System.Net.Http;
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
            var configurationToUse = _unusedDaemons.Dequeue();

            if (!IPAddress.TryParse(configurationToUse.IpAddress, out var parsedAddress))
            {
                throw new ArgumentException($"Unrecognized IP address format. {configurationToUse.IpAddress}.");
            }

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(parsedAddress, configurationToUse.Port);

            return new Point(tcpClient);
        }
    }
}