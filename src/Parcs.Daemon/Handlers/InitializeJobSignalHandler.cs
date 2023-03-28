using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Handlers
{
    public sealed class InitializeJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public InitializeJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public async Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            var jobId = await channel.ReadGuidAsync(cancellationToken);
            var workerModulesPath = await channel.ReadStringAsync(cancellationToken);
            _jobContextAccessor.Current?.CancellationTokenSource.Cancel();
            _jobContextAccessor.Set(jobId, workerModulesPath);
        }
    }
}