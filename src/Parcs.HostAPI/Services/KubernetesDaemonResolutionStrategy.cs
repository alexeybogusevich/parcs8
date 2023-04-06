using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services
{
    public class KubernetesDaemonResolutionStrategy : IDaemonResolutionStrategy
    {
        public IEnumerable<Daemon> Resolve()
        {
            throw new NotImplementedException();
        }
    }
}