using Parcs.Core.Models;

namespace Parcs.Core.Messaging
{
    /// <summary>
    /// Publishes point-creation requests to the broker the KEDA daemon scaler watches.
    /// </summary>
    public interface IPointRequestPublisher
    {
        /// <summary>False when no broker is configured (direct-TCP deployments).</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Publishes all <paramref name="requests"/> before returning, so the scaler sees the
        /// whole batch as queue depth at once and provisions daemons in parallel.
        /// </summary>
        Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default);
    }
}
