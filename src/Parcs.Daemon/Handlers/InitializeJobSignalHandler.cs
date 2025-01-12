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
                _logger.LogWarning("Job {JobId} already exists, exiting.", jobId);
                return;
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