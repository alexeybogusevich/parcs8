using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public class ConfigurationDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        private readonly DaemonsConfiguration _daemonsConfiguration;

        public ConfigurationDaemonResolutionStrategy(IOptions<DaemonsConfiguration> options)
        {
            _daemonsConfiguration = options.Value;
        }

        public Task<IEnumerable<Daemon>> ResolveAsync() => Task.FromResult(_daemonsConfiguration.PreconfiguredInstances);
    }
}