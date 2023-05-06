using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Data.Entities;
using Parcs.Core.Models;

namespace Parcs.HostAPI.Handlers
{
    public sealed class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobTracker _jobTracker;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileSaver _fileSaver;

        public CreateJobCommandHandler(
            ParcsDbContext parcsDbContext,
            IJobTracker jobTracker,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileSaver fileSaver)
        {
            _parcsDbContext = parcsDbContext;
            _jobTracker = jobTracker;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileSaver = fileSaver;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = new JobEntity
            {
                ModuleId = request.ModuleId,
                AssemblyName = request.AssemblyName,
                ClassName = request.ClassName,
            };

            await _parcsDbContext.Jobs.AddAsync(job, cancellationToken);
            await _parcsDbContext.JobStatuses.AddAsync(new JobStatusEntity(job.Id, (short)JobStatus.Created), cancellationToken);
            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            _jobTracker.StartTracking(job.Id);
            _ = _jobTracker.TryGetCancellationToken(job.Id, out var jobCancellationToken);

            var inputPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Input);
            await _fileSaver.SaveAsync(request.InputFiles, inputPath, jobCancellationToken);

            return new CreateJobCommandResponse(job.Id);
        }
    }
}