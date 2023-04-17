using Microsoft.Extensions.Options;
using Parcs.Shared.Configuration;
using Parcs.Shared.Models;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public class ConfigurationDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        private readonly DaemonsConfiguration _daemonsConfiguration;

        public ConfigurationDaemonResolutionStrategy(IOptions<DaemonsConfiguration> options)
        {
            _daemonsConfiguration = options.Value;
        }

        public IEnumerable<Daemon> Resolve() => _daemonsConfiguration.PreconfiguredInstances;
    }
}