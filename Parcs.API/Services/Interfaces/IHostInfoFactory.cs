using Parcs.Core;
using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IHostInfoFactory
    {
        IHostInfo Create(IEnumerable<Daemon> daemons);
    }
}