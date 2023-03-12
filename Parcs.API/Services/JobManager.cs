using Parcs.Core;
using Parcs.HostAPI.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.HostAPI.Services
{
    public class JobManager : IJobManager
    {
        private readonly ConcurrentDictionary<Guid, Job> _activeJobs = new ();

        public Job Create()
        {
            var job = new Job();
            _activeJobs.TryAdd(job.Id, job);
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