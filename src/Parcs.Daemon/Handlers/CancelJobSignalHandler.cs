using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace Parcs.Daemon.Handlers
{
    public class CancelJobSignalHandler(IJobContextAccessor jobContextAccessor, ILogger<CancelJobSignalHandler> logger) : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor = jobContextAccessor;
        private readonly ILogger<CancelJobSignalHandler> _logger = logger;

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();
            _logger.LogWarning("Attempting to cancel job '{JobId}'.", jobId);

            if (_jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                jobContext.CancellationTokenSource.Cancel();
                _logger.LogWarning("Job '{JobId}' cancelled successfully.", jobId);
            }
        }
    }
}