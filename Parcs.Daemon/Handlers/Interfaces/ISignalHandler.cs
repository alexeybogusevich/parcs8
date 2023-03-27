using Parcs.Net;

namespace Parcs.Daemon.Handlers.Interfaces
{
    public interface ISignalHandler
    {
        Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default);
    }
}