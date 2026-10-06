using Parcs.Core.Models;

namespace Parcs.Core.Messaging
{
    /// <summary>
    /// Used when no broker is configured: point creation falls back to direct TCP and daemons
    /// only serve connections on their TCP server.
    /// </summary>
    public sealed class DisabledPointQueue : IPointRequestPublisher, IPointRequestReceiver
    {
        public bool IsEnabled => false;

        public Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No point queue provider is configured.");

        public Task ReceiveOneAsync(Func<PointCreationRequest, CancellationToken, Task> handler, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No point queue provider is configured.");
    }
}
