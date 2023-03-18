using Parcs.Core;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public class JobCompletionObserver : IJobCompletionObserver, IObserver<JobCompletedEvent>
    {
        private readonly IJobManager _jobManager;
        private readonly ILogger<JobCompletionObserver> _logger;
        private readonly ConcurrentDictionary<Guid, IDisposable> _subscribedJobs = new();

        public JobCompletionObserver(IJobManager jobManager, ILogger<JobCompletionObserver> logger)
        {
            _jobManager = jobManager;
            _logger = logger;
        }

        public void Subscribe(Job job)
        {
            var subscription = job.Subscribe(this);
            _ = _subscribedJobs.TryAdd(job.Id, subscription);
        }

        public void OnNext(JobCompletedEvent @event)
        {
            _logger.LogInformation("Job {Id} is being disposed. Last status: {Status}.", @event.JobId, @event.JobStatus);

            if (_subscribedJobs.TryRemove(@event.JobId, out var jobSubscription))
            {
                jobSubscription.Dispose();
            }

            _ = _jobManager.TryRemove(@event.JobId);
        }

        public void OnCompleted() => _logger.LogInformation("Job communicated observation completion.");

        public void OnError(Exception ex) => _logger.LogError(ex, "Job communicated an error {Message}.", ex.Message);
    }
}