using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IPointCreationService
    {
        Task<IPoint> CreatePointAsync(long jobId, long moduleId, IDictionary<string, string> arguments, string daemonHostUrl, int daemonPort, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes <paramref name="count"/> point-requested Service Bus messages as a batch
        /// and awaits all daemon connections concurrently, so KEDA sees the full queue depth
        /// at once rather than one message at a time.
        /// </summary>
        Task<IPoint[]> CreatePointsAsync(int count, long jobId, long moduleId, IDictionary<string, string> arguments, CancellationToken cancellationToken = default);
    }
}