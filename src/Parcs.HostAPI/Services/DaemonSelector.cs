using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public sealed class DaemonSelector : IDaemonSelector
    {
        private readonly DefaultDaemonConfiguration _defaultDaemonConfiguration;

        public DaemonSelector(IOptions<DefaultDaemonConfiguration> options)
        {
            _defaultDaemonConfiguration = options.Value;
        }

        public IEnumerable<Daemon> Select(IEnumerable<Daemon> suppliedDaemons = null)
        {
            if (suppliedDaemons != null && suppliedDaemons.Any())
            {
                return suppliedDaemons;
            }

            return new List<Daemon>
            {
                new Daemon
                {
                    HostUrl = _defaultDaemonConfiguration.HostUrl,
                    Port = _defaultDaemonConfiguration.Port,
                }
            };
        }
    }
}