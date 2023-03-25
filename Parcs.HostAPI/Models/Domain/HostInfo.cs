using Parcs.Net;
using Parcs.Shared.Models;
using Parcs.TCP.Host.Models;
using System.Net.Sockets;

namespace Parcs.HostAPI.Models.Domain
{
    internal sealed class HostInfo : IHostInfo
    {
        private readonly Queue<Daemon> _availableDaemons;
        private readonly Job _job;

        public HostInfo(Job job, IEnumerable<Daemon> availableDaemons)
        {
            _job = job;
            _availableDaemons = new Queue<Daemon>(availableDaemons);
        }

        public int AvailablePointsNumber => _availableDaemons.Count;

        public async Task<IPoint> CreatePointAsync()
        {
            if (!_availableDaemons.TryDequeue(out var daemon))
            {
                throw new ArgumentException("No more points can be created.");
            }

            _job.TrackExecution(daemon);

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(daemon.HostUrl, daemon.Port);

            return new Point(tcpClient);
        }
    }
}