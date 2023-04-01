using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Shared.Models.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public class CancelJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public CancelJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            _jobContextAccessor.Current?.CancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
    }
}