using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IDirectoryPathBuilder
    {
        string Build(JobDirectoryGroup directoryGroup, Guid entityId);
    }
}