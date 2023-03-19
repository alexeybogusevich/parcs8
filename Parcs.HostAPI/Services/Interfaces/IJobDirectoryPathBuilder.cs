using Parcs.HostAPI.Models.Enums;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobDirectoryPathBuilder
    {
        string Build(Guid jobId);
        string Build(Guid jobId, JobDirectoryGroup directoryGroup);
    }
}