using Parcs.Net;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public class InitializeJobSignalHandler : ISignalHandler
    {
        public Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}