using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models.Constants;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public sealed class JobDirectoryPathBuilder(IOptions<FileSystemConfiguration> options) : IJobDirectoryPathBuilder
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration = options.Value;

        public string Build(long jobId)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Jobs, jobId.ToString());
        }

        public string Build(long jobId, JobDirectoryGroup directoryGroup)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Jobs, jobId.ToString(), directoryGroup.ToString());
        }
    }
}