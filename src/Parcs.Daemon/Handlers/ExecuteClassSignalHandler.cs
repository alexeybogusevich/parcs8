using Parcs.Daemon.Extensions;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;
using Microsoft.Extensions.Logging;

namespace Parcs.Daemon.Handlers
{
    public sealed class ExecuteClassSignalHandler(
        IJobContextAccessor jobContextAccessor,
        IModuleLoader moduleLoader,
        IModuleInfoFactory moduleInfoFactory,
        IHostApiClient hostApiClient,
        ILogger<ExecuteClassSignalHandler> logger) : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor = jobContextAccessor;
        private readonly IModuleLoader _moduleLoader = moduleLoader;
        private readonly IModuleInfoFactory _moduleInfoFactory = moduleInfoFactory;
        private readonly IHostApiClient _hostApiClient = hostApiClient;
        private readonly ILogger<ExecuteClassSignalHandler> _logger = logger;

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();
            _logger.LogInformation("Starting class execution for job {JobId}", jobId);

            if (!_jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                throw new ArgumentException($"A job with id {jobId} can't be found");
            }

            var (_, moduleId, arguments, jobCancellationToken) = jobContext;

            var jobMetadata = new JobMetadata(jobId, moduleId);
            var assemblyName = await managedChannel.ReadStringAsync();
            var className = await managedChannel.ReadStringAsync();

            _logger.LogInformation(
                "Starting class execution for job {JobId}. Assembly: {Assembly}, Class: {Class}", jobId, assemblyName, className);

            try
            {
                var module = _moduleLoader.Load(moduleId, assemblyName, className);

                await using var moduleInfo = _moduleInfoFactory.Create(jobMetadata, arguments, managedChannel, jobCancellationToken);
                await module.RunAsync(moduleInfo, jobCancellationToken);

                _logger.LogInformation("Class execution for job {JobId} succeeded.", jobId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Class execution for job {JobId} failed with message {Message}.", jobId, ex.Message);
                await _hostApiClient.PostJobFailureAsync(new(jobId, ex.Message, ex.StackTrace), CancellationToken.None);
            }
        }
    }
}