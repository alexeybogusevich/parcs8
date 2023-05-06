using Parcs.Core.Models.Enums;

namespace Parcs.Core.Services.Interfaces
{
    public interface IJobDirectoryPathBuilder
    {
        string Build(long jobId);
        string Build(long jobId, JobDirectoryGroup directoryGroup);
    }
}