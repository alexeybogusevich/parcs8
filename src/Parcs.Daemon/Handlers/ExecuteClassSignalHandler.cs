﻿using Parcs.Daemon.Extensions;
using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;

namespace Parcs.TCP.Daemon.Handlers
{
    public sealed class ExecuteClassSignalHandler(
        IJobContextAccessor jobContextAccessor, IModuleLoader moduleLoader, IModuleInfoFactory moduleInfoFactory, IHostApiClient hostApiClient) : ISignalHandler
    {
        private readonly IJobContextAccessor _jobContextAccessor = jobContextAccessor;
        private readonly IModuleLoader _moduleLoader = moduleLoader;
        private readonly IModuleInfoFactory _moduleInfoFactory = moduleInfoFactory;
        private readonly IHostApiClient _hostApiClient = hostApiClient;

        public async Task HandleAsync(IManagedChannel managedChannel, CancellationToken cancellationToken = default)
        {
            var jobId = await managedChannel.ReadLongAsync();

            if (!_jobContextAccessor.TryGet(jobId, out var jobContext))
            {
                throw new ArgumentException($"A job with id {jobId} can't be found");
            }

            var (_, moduleId, arguments, jobCancellationToken) = jobContext;

            var jobMetadata = new JobMetadata(jobId, moduleId);
            var assemblyName = await managedChannel.ReadStringAsync();
            var className = await managedChannel.ReadStringAsync();

            try
            {
                var module = _moduleLoader.Load(moduleId, assemblyName, className);
                await using var moduleInfo = _moduleInfoFactory.Create(jobMetadata, arguments, managedChannel, jobCancellationToken);
                await module.RunAsync(moduleInfo, jobCancellationToken);
            }
            catch (Exception ex)
            {
                await _hostApiClient.PostJobFailureAsync(new (jobId, ex.Message, ex.StackTrace), CancellationToken.None);
            }
        }
    }
}