using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;

namespace Parcs.Daemon.Handlers
{
    public class CancelJobSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;

        public CancelJobSignalHandler(IJobContextAccessor jobContextAccessor)
        {
            _jobContextAccessor = jobContextAccessor;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadGuidAsync();

            if (_jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                jobContext.CancellationTokenSource.Cancel();
            }
        }
    }
}