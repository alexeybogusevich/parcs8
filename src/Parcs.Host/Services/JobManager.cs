using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public sealed class JobManager : IJobManager
    {
        private readonly JobsConfiguration _jobsConfiguration;
        private readonly ConcurrentDictionary<Guid, Job> _activeJobs = new();
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public JobManager(
            IOptions<JobsConfiguration> options,
            IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileEraser fileEraser)
        {
            _jobsConfiguration = options.Value;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public Job Create(Guid moduleId, string assemblyName, string className)
        {
            if (_activeJobs.Count == _jobsConfiguration.MaximumActiveJobs)
            {
                throw new ArgumentException("Maximum number of active jobs reached. Consider deleting idle jobs.");
            }

            var modulePath = _moduleDirectoryPathBuilder.Build(moduleId);

            var job = new Job(moduleId, modulePath, assemblyName, className);
            _ = _activeJobs.TryAdd(job.Id, job);

            return job;
        }

        public bool TryGet(Guid id, out Job job) => _activeJobs.TryGetValue(id, out job);

        public bool TryRemove(Guid id)
        {
            if (!_activeJobs.TryRemove(id, out _))
            {
                return false;
            }

            var jobDirectoryPath = _jobDirectoryPathBuilder.Build(id);
            _fileEraser.TryDeleteRecursively(jobDirectoryPath);

            return true;
        }
    }
}