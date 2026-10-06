using Parcs.Core.Models;

namespace Parcs.Core.Messaging
{
    /// <summary>
    /// Daemon-side counterpart of <see cref="IPointRequestPublisher"/>. A KEDA-scaled daemon pod
    /// hosts exactly one point, so the receiver takes exactly one message.
    /// </summary>
    public interface IPointRequestReceiver
    {
        bool IsEnabled { get; }

        /// <summary>
        /// Waits for one point request and runs <paramref name="handler"/> on it, keeping the
        /// message leased for as long as the handler runs. The message is acknowledged when the
        /// handler completes and returned to the queue for redelivery when it throws (the
        /// exception is then rethrown). Malformed messages are discarded and waiting continues.
        /// </summary>
        Task ReceiveOneAsync(Func<PointCreationRequest, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
    }
}
