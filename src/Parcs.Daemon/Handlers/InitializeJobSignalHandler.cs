using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Shared.Models.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public sealed class InitializeJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public InitializeJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadGuidAsync();
            var workerModulesPath = await managedChannel.ReadStringAsync();

            _jobContextAccessor.Current?.CancellationTokenSource.Cancel();
            _jobContextAccessor.Set(jobId, workerModulesPath);

            managedChannel.SetCancellation(_jobContextAccessor.Current.CancellationTokenSource.Token);
        }
    }
}