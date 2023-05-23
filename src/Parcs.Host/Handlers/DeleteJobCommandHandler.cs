using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Parcs.Host.Handlers
{
    public sealed class DeleteJobCommandHandler : IRequestHandler<DeleteJobCommand>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobTracker _jobTracker;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public DeleteJobCommandHandler(
            ParcsDbContext parcsDbContext, IJobTracker jobTracker, IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileEraser fileEraser)
        {
            _parcsDbContext = parcsDbContext;
            _jobTracker = jobTracker;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public async Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs.FirstOrDefaultAsync(e => e.Id == request.JobId, CancellationToken.None);

            if (job is null)
            {
                return;
            }

            await _jobTracker.CancelAndStopTrackingAsync(request.JobId);

            _parcsDbContext.Jobs.Remove(job);
            await _parcsDbContext.SaveChangesAsync(CancellationToken.None);

            _fileEraser.TryDeleteRecursively(_jobDirectoryPathBuilder.Build(job.Id));
        }
    }
}