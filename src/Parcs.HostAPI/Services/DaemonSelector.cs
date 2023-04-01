using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class DaemonSelector : IDaemonSelector
    {
        private readonly DaemonsConfiguration _daemonsConfiguration;

        public DaemonSelector(IOptions<DaemonsConfiguration> options)
        {
            _daemonsConfiguration = options.Value;
        }

        public IEnumerable<Daemon> Select(int? requestedNumber)
        {
            requestedNumber ??= 1;

            var actualNumber = _daemonsConfiguration.PreconfiguredInstances.Count();

            if (requestedNumber > actualNumber)
            {
                throw new ArgumentException($"Not enough daemons ({actualNumber}) to satisfy the request {requestedNumber}.");
            }

            return _daemonsConfiguration.PreconfiguredInstances;
        }
    }
}