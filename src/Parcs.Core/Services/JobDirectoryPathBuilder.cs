using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Models.Constants;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
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