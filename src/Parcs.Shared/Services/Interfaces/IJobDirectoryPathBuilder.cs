using Parcs.Shared.Models.Enums;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IJobDirectoryPathBuilder
    {
        string Build(Guid jobId);
        string Build(Guid jobId, JobDirectoryGroup directoryGroup);
    }
}