using Parcs.Core.Services.Interfaces;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    /// <summary>
    /// Daemons have no access to the job database, so nested points inherit the pool (CPU/GPU)
    /// of the point request this pod is serving.
    /// </summary>
    public sealed class InheritedJobPlacementResolver(ICurrentPointRequestAccessor currentPointRequestAccessor) : IJobPlacementResolver
    {
        private readonly ICurrentPointRequestAccessor _currentPointRequestAccessor = currentPointRequestAccessor;

        public Task<bool> RequiresGpuAsync(long jobId, CancellationToken cancellationToken = default)
        {
            var current = _currentPointRequestAccessor.Current;
            return Task.FromResult(current is not null && current.JobId == jobId && current.RequiresGpu);
        }
    }
}
