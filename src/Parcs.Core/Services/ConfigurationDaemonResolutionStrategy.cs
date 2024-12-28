using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public class ConfigurationDaemonResolutionStrategy(IOptions<DaemonsConfiguration> options) : IDaemonResolutionStrategy
    {
        private readonly DaemonsConfiguration _daemonsConfiguration = options.Value;

        public IEnumerable<Daemon> Resolve() => _daemonsConfiguration.PreconfiguredInstances;
    }
}