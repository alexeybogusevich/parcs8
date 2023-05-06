using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.Daemon.Services
{
    public class JobContextAccessor : IJobContextAccessor
    {
        private readonly ConcurrentDictionary<long, JobContext> _activeContexts = new();

        public void Add(long jobId, long moduleId, int pointsNumber, IDictionary<string, string> arguments)
        {
            _ = _activeContexts.TryAdd(jobId, new JobContext(jobId, moduleId, pointsNumber, arguments));
        }

        public bool TryGet(long jobId, out JobContext jobContext)
        {
            return _activeContexts.TryGetValue(jobId, out jobContext);
        }

        public void Remove(long jobId)
        {
            _ = _activeContexts.Remove(jobId, out _);
        }
    }
}