using Parcs.Shared.Models;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IDaemonsResolver
    {
        bool AnyAvailableDaemons();
        IEnumerable<Daemon> GetAvailableDaemons();
    }
}