using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    public sealed class DefaultSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}