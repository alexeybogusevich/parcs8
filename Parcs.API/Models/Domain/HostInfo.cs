using Parcs.Core;
using Parcs.TCP.Host.Models;

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

        public IPoint CreatePoint()
        {
            var configurationToUse = _unusedDaemons.Dequeue();
            return new Point(configurationToUse.IpAddress, configurationToUse.Port);
        }
    }
}