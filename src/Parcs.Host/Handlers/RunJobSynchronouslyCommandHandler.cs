using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Parcs.Host.Handlers
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
            var job = await _parcsDbContext.Jobs
                .Include(e => e.Statuses)
                .FirstOrDefaultAsync(e => e.Id == command.JobId, cancellationToken) ?? throw new ArgumentException($"Job not found.");
            
            if (job.Statuses.LastOrDefault()?.Status != (short)JobStatus.Created)
            {
                throw new ArgumentException("The job has already been run.");
            }

            if (!_jobTracker.TryGetCancellationToken(job.Id, out var jobCancellationToken))
            {
                _jobTracker.StartTracking(job.Id);
                _ = _jobTracker.TryGetCancellationToken(job.Id, out jobCancellationToken);
            }

            var jobMetadata = new JobMetadata(job.Id, job.ModuleId);

            try
            {
                var module = _moduleLoader.Load(job.ModuleId, job.AssemblyName, job.ClassName);
                await using var moduleInfo = _moduleInfoFactory.Create(jobMetadata, command.PointsNumber, command.Arguments, jobCancellationToken);

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

            return new RunJobSynchronouslyCommandResponse(jobLastStatus);
        }
    }
}