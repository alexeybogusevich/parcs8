using Parcs.Shared;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobManager
    {
        Job Create(Guid moduleId, string assemblyName, string className);
        bool TryGet(Guid id, out Job job);
        bool TryRemove(Guid id);
    }
}