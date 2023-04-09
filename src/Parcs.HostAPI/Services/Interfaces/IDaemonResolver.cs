using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonResolver
    {
        public IEnumerable<Daemon> GetAvailableDaemons();
    }
}