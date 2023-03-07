using Parcs.Core;
using Parcs.TCP.Host.Configuration;

namespace Parcs.TCP.Host.Models
{
    internal sealed class HostInfo : IHostInfo
    {
        private readonly Queue<DaemonConfiguration> _unusedConfigurations;
        private readonly int _initialConfigurationsNumber;

        public HostInfo(IEnumerable<DaemonConfiguration> daemonConfigurations)
        {
            _unusedConfigurations = new Queue<DaemonConfiguration>(daemonConfigurations);
            _initialConfigurationsNumber = daemonConfigurations.Count();
        }

        public int MaximumPointsNumber => _initialConfigurationsNumber;

        public IPoint CreatePoint()
        {
            var configurationToUse = _unusedConfigurations.Dequeue();
            return new Point(configurationToUse.IpAddress, configurationToUse.Port);
        }
    }
}