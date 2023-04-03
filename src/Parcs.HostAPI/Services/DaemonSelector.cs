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

        public IEnumerable<Daemon> Select(int requestedNumber)
        {
            var availableNumber = _daemonsConfiguration.PreconfiguredInstances.Count();

            if (requestedNumber > availableNumber)
            {
                throw new ArgumentException($"Not enough daemons ({availableNumber}) to satisfy the request ({requestedNumber}).");
            }

            return _daemonsConfiguration.PreconfiguredInstances.Take(requestedNumber);
        }
    }
}