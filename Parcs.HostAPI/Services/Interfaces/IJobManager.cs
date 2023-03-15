using Parcs.Core;
namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobManager
    {
        Job Create(Guid moduleId);
        bool TryGet(Guid id, out Job job);
        bool TryRemove(Guid id);
    }
}