using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Data.Entities;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Handlers
{
    public class CloneJobCommandHandler : IRequestHandler<CloneJobCommand, CloneJobCommandResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobTracker _jobTracker;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileMover _fileMover;

        public CloneJobCommandHandler(
            ParcsDbContext parcsDbContext,
            IJobTracker jobTracker,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileMover fileMover)
        {
            _parcsDbContext = parcsDbContext;
            _jobTracker = jobTracker;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileMover = fileMover;
        }

        public async Task<CloneJobCommandResponse> Handle(CloneJobCommand request, CancellationToken cancellationToken)
        {
            var originalJob = await _parcsDbContext.Jobs.FirstOrDefaultAsync(e => e.Id == request.JobId) ?? throw new ArgumentException("Job not found");
            
            var newJob = new JobEntity
            {
                ModuleId = originalJob.ModuleId,
                AssemblyName = originalJob.AssemblyName,
                ClassName = originalJob.ClassName,
                Statuses = new List<JobStatusEntity>
                {
                    new JobStatusEntity { Status = (short)JobStatus.Created },
                },
            };

            await _parcsDbContext.Jobs.AddAsync(newJob, cancellationToken);
            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            _jobTracker.StartTracking(newJob.Id);
            _ = _jobTracker.TryGetCancellationToken(newJob.Id, out var jobCancellationToken);

            var originalInputPath = _jobDirectoryPathBuilder.Build(originalJob.Id, JobDirectoryGroup.Input);
            var newInputPath = _jobDirectoryPathBuilder.Build(newJob.Id, JobDirectoryGroup.Input);

            _fileMover.Copy(originalInputPath, newInputPath);

            return new CloneJobCommandResponse(newJob.Id);
        }
    }
}