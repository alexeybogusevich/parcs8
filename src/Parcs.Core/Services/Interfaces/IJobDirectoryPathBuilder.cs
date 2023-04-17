using Parcs.Core.Models.Enums;

namespace Parcs.Core.Services.Interfaces
{
    public interface IJobDirectoryPathBuilder
    {
        string Build(Guid jobId);
        string Build(Guid jobId, JobDirectoryGroup directoryGroup);
    }
}