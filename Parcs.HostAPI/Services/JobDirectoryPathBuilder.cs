using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Constants;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Services
{
    public class JobDirectoryPathBuilder : IJobDirectoryPathBuilder
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