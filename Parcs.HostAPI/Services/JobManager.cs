using Microsoft.Extensions.Options;
using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public class JobManager : IJobManager, IObserver<JobCompletedEvent>
    {
        private readonly JobsConfiguration _jobsConfiguration;

        private readonly ConcurrentDictionary<Guid, Job> _activeJobs = new();
        private readonly ConcurrentDictionary<Guid, IDisposable> _subscribedJobs = new();

        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;
        private readonly ILogger<JobManager> _logger;

        public JobManager(
            IOptions<JobsConfiguration> options,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileEraser fileEraser,
            ILogger<JobManager> logger)
        {
            _jobsConfiguration = options.Value;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileEraser = fileEraser;
            _logger = logger;
        }

        public Job Create(Guid moduleId, string assemblyName, string className)
        {
            if (_activeJobs.Count == _jobsConfiguration.MaximumActiveJobs)
            {
                throw new ArgumentException("Maximum number of active jobs reached. Consider deleting idle jobs.");
            }

            var job = new Job(moduleId, assemblyName, className);
            _ = _activeJobs.TryAdd(job.Id, job);

            var subscription = job.Subscribe(this);
            _ = _subscribedJobs.TryAdd(job.Id, subscription);

            return job;
        }

        public bool TryGet(Guid id, out Job job) => _activeJobs.TryGetValue(id, out job);

        public bool TryRemove(Guid id) => _activeJobs.TryRemove(id, out _);

        public void OnNext(JobCompletedEvent @event)
        {
            _logger.LogInformation("Job {Id} is being disposed. Last status: {Status}.", @event.JobId, @event.JobStatus);

            if (_subscribedJobs.TryRemove(@event.JobId, out var jobSubscription))
            {
                jobSubscription.Dispose();
            }

            _ = _activeJobs.TryRemove(@event.JobId, out _);

            var jobDirectoryPath = _jobDirectoryPathBuilder.Build(@event.JobId);

            _fileEraser.TryDeleteRecursively(jobDirectoryPath);
        }

        public void OnCompleted() => _logger.LogInformation("Job communicated observation completion.");

        public void OnError(Exception ex) => _logger.LogError(ex, "Job communicated an error {Message}.", ex.Message);
    }
}