using Parcs.Core.Models.Interfaces;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IChannelOrchestrator
    {
        Task OrchestrateAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default);
    }
}