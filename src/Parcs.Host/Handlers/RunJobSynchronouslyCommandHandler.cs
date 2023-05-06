using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Parcs.HostAPI.Handlers
{
    public class RunJobSynchronouslyCommandHandler : IRequestHandler<RunJobSynchronouslyCommand, RunJobSynchronouslyCommandResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IModuleInfoFactory _moduleInfoFactory;
        private readonly IJobTracker _jobTracker;
        private readonly IModuleLoader _moduleLoader;

        public RunJobSynchronouslyCommandHandler(
            ParcsDbContext parcsDbContext, IModuleInfoFactory moduleInfoFactory, IJobTracker jobTracker, IModuleLoader moduleLoader)
        {
            _parcsDbContext = parcsDbContext;
            _moduleInfoFactory = moduleInfoFactory;
            _jobTracker = jobTracker;
            _moduleLoader = moduleLoader;
        }

        public async Task<RunJobSynchronouslyCommandResponse> Handle(RunJobSynchronouslyCommand command, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs.FirstOrDefaultAsync(cancellationToken);

            ArgumentNullException.ThrowIfNull(job);

            if (!_jobTracker.TryGetCancellationToken(job.Id, out var jobCancellationToken))
            {
                throw new ArgumentException("Job cancellation token was null.");
            }

            var jobMetadata = new JobMetadata(job.Id, job.ModuleId);
            var arguments = command.GetArgumentsDictionary();
            var pointsNumber = command.PointsNumber;

            try
            {
                var module = _moduleLoader.Load(job.ModuleId, job.AssemblyName, job.ClassName);
                await using var moduleInfo = _moduleInfoFactory.Create(jobMetadata, pointsNumber, arguments, jobCancellationToken);

                await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Started), CancellationToken.None);
                await _parcsDbContext.SaveChangesAsync(CancellationToken.None);

                await module.RunAsync(moduleInfo, jobCancellationToken);

                await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Completed), CancellationToken.None);
                await _parcsDbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Cancelled), CancellationToken.None);
                await _parcsDbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                await _parcsDbContext.JobFailures.AddAsync(new(job.Id, ex.Message, ex.StackTrace), CancellationToken.None);
                await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Failed), CancellationToken.None);
                await _parcsDbContext.SaveChangesAsync(CancellationToken.None);
            }

            var jobLastStatus = (JobStatus?)job.Statuses.LastOrDefault()?.Status;

            return new RunJobSynchronouslyCommandResponse(job.Id, jobLastStatus);
        }
    }
}