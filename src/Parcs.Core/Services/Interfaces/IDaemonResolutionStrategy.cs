using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    public interface IDaemonResolutionStrategy
    {
        IEnumerable<Daemon> Resolve();
    }
}