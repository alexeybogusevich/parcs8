using Microsoft.Extensions.Logging;
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
        private readonly ILogger<DaemonResolver> _logger;

        public DaemonResolver(
            IOptions<HostingConfiguration> hostingOptions,
            IDaemonResolutionStrategyFactory daemonResolutionStrategyFactory,
            ILogger<DaemonResolver> logger)
        {
            _hostingConfiguration = hostingOptions.Value;
            _daemonResolutionStrategyFactory = daemonResolutionStrategyFactory;
            _logger = logger;
        }

        public bool AnyAvailableDaemons()
        {
            var availableDaemons = GetAvailableDaemons();
            return availableDaemons is not null && availableDaemons.Any();
        }

        public IEnumerable<Daemon> GetAvailableDaemons()
        {
            var resolutionStrategy = _daemonResolutionStrategyFactory.Create(_hostingConfiguration.Environment);

            var resolvedDaemons = resolutionStrategy.Resolve();

            if (resolvedDaemons is null || !resolvedDaemons.Any())
            {
                throw new InvalidOperationException($"No daemon was resolved. Strategy: {resolutionStrategy.GetType().Name}");
            }

            _logger.LogInformation("Resolved daemons: {Daemons}", string.Join(',', resolvedDaemons.Select(d => d.HostUrl)));

            return resolvedDaemons;
        }
    }
}