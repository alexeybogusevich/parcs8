using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    public interface IDaemonResolver
    {
        bool AnyAvailableDaemons();
        IEnumerable<Daemon> GetAvailableDaemons();
    }
}