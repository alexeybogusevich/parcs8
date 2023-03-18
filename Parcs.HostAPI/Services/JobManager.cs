using Microsoft.Extensions.Options;
using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public class JobManager : IJobManager, IObserver<JobCompletionNotification>
    {
        private readonly JobsConfiguration _jobsConfiguration;
        private readonly ILogger<JobManager> _logger;
        private readonly ConcurrentDictionary<Guid, IDisposable> _subscribedJobs = new();
        private readonly ConcurrentDictionary<Guid, Job> _activeJobs = new();

        public JobManager(IOptions<JobsConfiguration> options, ILogger<JobManager> logger)
        {
            _logger = logger;
            _jobsConfiguration = options.Value;
        }

        public Job Create(Guid moduleId)
        {
            if (_activeJobs.Count == _jobsConfiguration.MaximumActiveJobs)
            {
                throw new ArgumentException("Maximum number of active jobs reached. Consider deleting idle jobs.");
            }

            var job = new Job(moduleId);
            _ = _activeJobs.TryAdd(job.Id, job);

            var subscription = job.Subscribe(this);
            _ = _subscribedJobs.TryAdd(job.Id, subscription);

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

        public void OnNext(JobCompletionNotification notification)
        {
            _logger.LogInformation("Job {Id} is being disposed. Last status: {Status}.", notification.JobId, notification.JobStatus);
                        
            if (_subscribedJobs.TryGetValue(notification.JobId, out var jobSubscription))
            {
                jobSubscription.Dispose();
            }

            _ = _activeJobs.TryRemove(notification.JobId, out _);
        }

        public void OnCompleted() => _logger.LogInformation("Job completed.");

        public void OnError(Exception ex) => _logger.LogError(ex, "Job failed with an error {Message}.", ex.Message);
    }
}