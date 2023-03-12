using Parcs.Core;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class DaemonSelector : IDaemonSelector
    {
        public IEnumerable<Daemon> Select(IEnumerable<Daemon> suppliedDaemons = null)
        {
            if (suppliedDaemons == null)
            {
                throw new ArgumentException("No daemons to run the module on.");
            }

            return suppliedDaemons;
        }
    }
}