﻿using Parcs.Daemon.Extensions;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;

namespace Parcs.TCP.Daemon.Handlers
{
    public sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor;
        private readonly IModuleLoader _moduleLoader;
        private readonly IModuleInfoFactory _moduleInfoFactory;
        private readonly IHostApiClient _hostApiClient;

        public ExecuteClassSignalHandler(
            IJobContextAccessor jobContextAccessor, IModuleLoader moduleLoader, IModuleInfoFactory moduleInfoFactory, IHostApiClient hostApiClient)
        {
            _jobContextAccessor = jobContextAccessor;
            _moduleLoader = moduleLoader;
            _moduleInfoFactory = moduleInfoFactory;
            _hostApiClient = hostApiClient;
        }

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadGuidAsync();

            if (!_jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                throw new ArgumentException($"A job with id {jobId} can't be found");
            }

            var (_, moduleId, pointsNumber, arguments, jobCancellationToken) = jobContext;

            var jobMetadata = new JobMetadata(jobId, moduleId);

            try
            {
                var assemblyName = await managedChannel.ReadStringAsync();
                var className = await managedChannel.ReadStringAsync();

                var module = _moduleLoader.Load(moduleId, assemblyName, className);
                var moduleInfo = _moduleInfoFactory.Create(jobMetadata, pointsNumber, arguments, managedChannel, jobCancellationToken);

                await module.RunAsync(moduleInfo, jobCancellationToken);
            }
            catch (Exception ex)
            {
                jobContext.TrackException(ex);

                await _hostApiClient.PutCancelJobAsync(jobId, CancellationToken.None);
            }
        }
    }
}