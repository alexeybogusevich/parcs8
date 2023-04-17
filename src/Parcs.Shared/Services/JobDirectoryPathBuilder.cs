using Microsoft.Extensions.Options;
using Parcs.Shared.Configuration;
using Parcs.Shared.Models.Constants;
using Parcs.Shared.Models.Enums;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public sealed class JobDirectoryPathBuilder : IJobDirectoryPathBuilder
    {
        private readonly FileSystemConfiguration _fileSystemConfiguration;

        public JobDirectoryPathBuilder(IOptions<FileSystemConfiguration> options)
        {
            _fileSystemConfiguration = options.Value;
        }

        public string Build(Guid jobId)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Jobs, jobId.ToString());
        }

        public string Build(Guid jobId, JobDirectoryGroup directoryGroup)
        {
            return Path.Combine(_fileSystemConfiguration.BasePath, BaseDirectory.Jobs, jobId.ToString(), directoryGroup.ToString());
        }
    }
}