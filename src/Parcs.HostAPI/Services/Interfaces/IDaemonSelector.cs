using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonSelector
    {
        IEnumerable<Daemon> Select(int? requestedNumber);
    }
}