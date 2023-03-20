using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IHostInfoFactory
    {
        IHostInfo Create(Job job, IEnumerable<Daemon> daemons);
    }
}