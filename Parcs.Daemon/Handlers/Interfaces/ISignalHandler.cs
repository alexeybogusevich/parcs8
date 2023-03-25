using Parcs.Net;

namespace Parcs.Daemon.Handlers.Interfaces
{
    internal interface ISignalHandler
    {
        Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}