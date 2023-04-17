using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class DaemonResolver : IDaemonResolver
    {
        private readonly HostingConfiguration _hostingConfiguration;
        private readonly IDaemonResolutionStrategyFactory _daemonResolutionStrategyFactory;

        public DaemonResolver(
            IOptions<HostingConfiguration> hostingOptions, IDaemonResolutionStrategyFactory daemonResolutionStrategyFactory)
        {
            _hostingConfiguration = hostingOptions.Value;
            _daemonResolutionStrategyFactory = daemonResolutionStrategyFactory;
        }

        public bool AnyAvailableDaemons()
        {
            var availableDaemons = GetAvailableDaemons();
            return availableDaemons is not null && availableDaemons.Any();
        }

        public IEnumerable<Daemon> GetAvailableDaemons()
        {
            var resolutionStrategy = _daemonResolutionStrategyFactory.Create(_hostingConfiguration.Environment);
            return resolutionStrategy.Resolve();
        }
    }
}