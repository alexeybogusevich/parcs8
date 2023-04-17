using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
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