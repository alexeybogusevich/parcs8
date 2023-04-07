using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonResolutionStrategy
    {
        Task<IEnumerable<Daemon>> ResolveAsync();
    }
}