using Parcs.Core.Models;

namespace Parcs.Core.Services.Interfaces
{
    public interface IDaemonsResolver
    {
        bool AnyAvailableDaemons();
        IEnumerable<Daemon> GetAvailableDaemons();
    }
}