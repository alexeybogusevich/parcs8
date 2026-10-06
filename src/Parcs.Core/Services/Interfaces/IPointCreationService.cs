using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IPointCreationService
    {
        /// <summary>
        /// False when no point queue is configured; callers then fall back to direct TCP to
        /// pre-provisioned daemons.
        /// </summary>
        bool IsEnabled { get; }

        Task<IPoint> CreatePointAsync(long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes <paramref name="count"/> point requests as a batch and awaits all daemon
        /// connections concurrently, so KEDA sees the full queue depth at once rather than one
        /// message at a time.
        /// </summary>
        Task<IPoint[]> CreatePointsAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default);
    }
}
