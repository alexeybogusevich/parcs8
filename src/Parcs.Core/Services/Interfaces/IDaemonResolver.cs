using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    public interface IDaemonResolver
    {
        IEnumerable<Daemon> GetAvailableDaemons();
    }
}