using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.Daemon.Services
{
    public class JobContextAccessor : IJobContextAccessor
    {
        private readonly ConcurrentDictionary<Guid, JobContext> _activeContexts = new();

        public void Add(Guid jobId, Guid moduleId, int pointsNumber, IDictionary<string, string> arguments)
        {
            _ = _activeContexts.TryAdd(jobId, new JobContext(jobId, moduleId, pointsNumber, arguments));
        }

        public bool TryGet(Guid jobId, out JobContext jobContext)
        {
            return _activeContexts.TryGetValue(jobId, out jobContext);
        }

        public void Remove(Guid jobId)
        {
            _ = _activeContexts.Remove(jobId, out _);
        }
    }
}