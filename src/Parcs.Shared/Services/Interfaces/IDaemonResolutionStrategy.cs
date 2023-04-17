using Parcs.Shared.Models;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IDaemonResolutionStrategy
    {
        IEnumerable<Daemon> Resolve();
    }
}