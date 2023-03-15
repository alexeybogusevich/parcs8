using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.Daemon.Handlers
{
    internal class DefaultSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}