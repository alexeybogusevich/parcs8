using Parcs.Core;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class DaemonSelector : IDaemonSelector
    {
        public IEnumerable<Daemon> Select(IEnumerable<Daemon> suppliedDaemons = null)
        {
            suppliedDaemons = new List<Daemon>
            {
                new Daemon
                {
                    IpAddress = "172.17.0.2",
                    Port = 1111,
                },
            };

            if (suppliedDaemons == null)
            {
                throw new ArgumentException("No daemons to run the module on.");
            }

            return suppliedDaemons;
        }
    }
}