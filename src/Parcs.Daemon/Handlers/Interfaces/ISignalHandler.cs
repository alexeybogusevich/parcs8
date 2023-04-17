using Parcs.Core.Models.Interfaces;

namespace Parcs.Daemon.Handlers.Interfaces
{
    public interface ISignalHandler
    {
        Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default);
    }
}