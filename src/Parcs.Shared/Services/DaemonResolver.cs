using Microsoft.Extensions.Options;
using Parcs.Shared.Configuration;
using Parcs.Shared.Models;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public sealed class DaemonResolver : IDaemonsResolver
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