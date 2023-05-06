﻿using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;

namespace Parcs.HostAPI.Handlers
{
    public class RunJobSynchronouslyCommandHandler : IRequestHandler<RunJobSynchronouslyCommand, RunJobSynchronouslyCommandResponse>
    {
        private readonly IModuleInfoFactory _moduleInfoFactory;
        private readonly IJobManager _jobManager;

        public RunJobSynchronouslyCommandHandler(IModuleInfoFactory moduleInfoFactory, IJobManager jobManager)
        {
            _moduleInfoFactory = moduleInfoFactory;
            _jobManager = jobManager;
        }

        public async Task<RunJobSynchronouslyCommandResponse> Handle(RunJobSynchronouslyCommand command, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(command.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {command.JobId}");
            }

            ArgumentNullException.ThrowIfNull(job.Module);

            var jobMetadata = new JobMetadata(job.Id, job.ModuleId);
            var arguments = command.GetArgumentsDictionary();
            var pointsNumber = command.PointsNumber;

            try
            {
                await using var moduleInfo = _moduleInfoFactory.Create(jobMetadata, pointsNumber, arguments, job.CancellationToken);

                job.Start();
                await job.Module.RunAsync(moduleInfo, job.CancellationToken);
                job.Finish();
            }
            catch (Exception ex)
            {
                job.Fail(ex.Message);
            }

            return new RunJobSynchronouslyCommandResponse(job);
        }
    }
}