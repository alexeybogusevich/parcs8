using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Data.Entities;
using Parcs.Core.Models;

namespace Parcs.Host.Handlers
{
    public sealed class CreateJobCommandHandler(
        ParcsDbContext parcsDbContext,
        IJobTracker jobTracker,
        IJobDirectoryPathBuilder jobDirectoryPathBuilder,
        IFileSaver fileSaver) : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;
        private readonly IJobTracker _jobTracker = jobTracker;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        private readonly IFileSaver _fileSaver = fileSaver;

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = new JobEntity
            {
                ModuleId = request.ModuleId,
                AssemblyName = request.AssemblyName,
                ClassName = request.ClassName,
                Statuses =
                [
                    new JobStatusEntity { Status = (short)JobStatus.Created },
                ],
            };

            await _parcsDbContext.Jobs.AddAsync(job, cancellationToken);
            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            _jobTracker.StartTracking(job.Id);
            _ = _jobTracker.TryGetCancellationToken(job.Id, out var jobCancellationToken);

            var inputPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Input);
            await _fileSaver.SaveAsync(request.InputFiles, inputPath, jobCancellationToken);

            return new CreateJobCommandResponse(job.Id);
        }
    }
}