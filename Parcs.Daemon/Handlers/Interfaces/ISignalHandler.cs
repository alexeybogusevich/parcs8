using Parcs.Net;

namespace Parcs.TCP.Daemon.Handlers.Interfaces
{
    internal interface ISignalHandler
    {
        Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}