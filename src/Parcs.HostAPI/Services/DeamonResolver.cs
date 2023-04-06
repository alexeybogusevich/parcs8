using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class DeamonResolver : IDaemonResolver
    {
        private readonly HostingConfiguration _hostingConfiguration;
        private readonly IDaemonResolutionStrategyFactory _daemonResolutionStrategyFactory;

        public DeamonResolver(
            IOptions<HostingConfiguration> hostingOptions, IDaemonResolutionStrategyFactory daemonResolutionStrategyFactory)
        {
            _hostingConfiguration = hostingOptions.Value;
            _daemonResolutionStrategyFactory = daemonResolutionStrategyFactory;
        }

        public IEnumerable<Daemon> GetAvailableDaemons()
        {
            var resolutionStrategy = _daemonResolutionStrategyFactory.Create(_hostingConfiguration.Environment);
            return resolutionStrategy.Resolve();
        }
    }
}