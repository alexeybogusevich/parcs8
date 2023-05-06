using Parcs.Host.Services.Interfaces;
using System.Collections.Concurrent;

namespace Parcs.Host.Services
{
    public sealed class JobTracker : IJobTracker
    {
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _trackedJobs = new();

        public void StartTracking(long jobId)
        {
            _ = _trackedJobs.TryAdd(jobId, new CancellationTokenSource());
        }

        public bool TryGetCancellationToken(long jobId, out CancellationToken cancellationToken)
        {
            if (_trackedJobs.TryGetValue(jobId, out var cancellationTokenSource))
            {
                cancellationToken = cancellationTokenSource.Token;

                return true;
            }

            cancellationToken = CancellationToken.None;

            return false;
        }

        public bool CancelAndStopTrackning(long jobId)
        {
            if (!_trackedJobs.TryGetValue(jobId, out var cancellationTokenSource))
            {
                return false;
            }

            cancellationTokenSource.Cancel();

            return StopTracking(jobId);
        }

        public bool StopTracking(long jobId) => _trackedJobs.TryRemove(jobId, out _);
    }
}