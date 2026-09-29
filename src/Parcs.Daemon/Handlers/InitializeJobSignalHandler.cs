using Parcs.Daemon.Exceptions;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace Parcs.Daemon.Handlers
{
    public sealed class InitializeJobSignalHandler(
        IJobContextAccessor jobContextAccessor, ILogger<InitializeJobSignalHandler> logger) : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor = jobContextAccessor;
        private readonly ILogger<InitializeJobSignalHandler> _logger = logger;

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();
            _logger.LogInformation("Attempting to initialize job {JobId}", jobId);

            var isExistingJob = _jobContextAccessor.TryGet(jobId, out _);
            await managedChannel.WriteDataAsync(isExistingJob);

            if (isExistingJob)
            {
                // This pod already owns jobId — it must have received a second, concurrently
                // delivered Pub/Sub message for the same job (see PointCreationConsumer). Throw
                // rather than return so the message gets Nacked and Pub/Sub redelivers it to a
                // pod that isn't already busy, instead of silently orphaning this connection.
                _logger.LogWarning("Job {JobId} already exists on this pod; Nacking for redelivery.", jobId);
                throw new DuplicateJobException(jobId);
            }

            var moduleId = await managedChannel.ReadLongAsync();
            var arguments = await managedChannel.ReadObjectAsync<IDictionary<string, string>>();

            _logger.LogDebug(
                "Initializing job {JobId}. ModuleId: {ModuleId}, Arguments: {Arguments}",
                jobId,
                moduleId,
                arguments.ToString());

            _jobContextAccessor.Add(jobId, moduleId, arguments);
            _ = _jobContextAccessor.TryGet(jobId, out var jobContext);

            managedChannel.SetCancellation(jobContext.CancellationTokenSource.Token);
        }
    }
}