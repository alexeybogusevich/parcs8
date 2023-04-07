using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDaemonResolver
    {
        Task<IEnumerable<Daemon>> GetAvailableDaemonsAsync();
    }
}