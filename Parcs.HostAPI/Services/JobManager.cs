using Microsoft.Extensions.Options;
using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public class JobManager : IJobManager
    {
        private readonly JobsConfiguration _jobsConfiguration;
        private readonly ConcurrentDictionary<Guid, Job> _activeJobs = new();
        private readonly IJobCompletionObserver _jobCompletionObserver;

        public JobManager(IJobCompletionObserver jobCompletionObserver, IOptions<JobsConfiguration> options)
        {
            _jobsConfiguration = options.Value;
            _jobCompletionObserver = jobCompletionObserver;
        }

        public Job Create(Guid moduleId, string assemblyName, string className)
        {
            if (_activeJobs.Count == _jobsConfiguration.MaximumActiveJobs)
            {
                throw new ArgumentException("Maximum number of active jobs reached. Consider deleting idle jobs.");
            }

            var job = new Job(moduleId, assemblyName, className);
            _ = _activeJobs.TryAdd(job.Id, job);

            _jobCompletionObserver.Subscribe(job);

            return job;
        }

        public bool TryGet(Guid id, out Job job)
        {
            return _activeJobs.TryGetValue(id, out job);
        }

        public bool TryRemove(Guid id)
        {
            return _activeJobs.TryRemove(id, out _);
        }
    }
}