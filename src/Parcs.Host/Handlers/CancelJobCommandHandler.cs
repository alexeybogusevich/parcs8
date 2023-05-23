using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Handlers
{
    public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobTracker _jobTracker;

        public CancelJobCommandHandler(ParcsDbContext parcsDbContext, IJobTracker jobTracker)
        {
            _parcsDbContext = parcsDbContext;
            _jobTracker = jobTracker;
        }

        public async Task Handle(CancelJobCommand request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs
                .Include(e => e.Statuses)
                .FirstOrDefaultAsync(e => e.Id == request.JobId, CancellationToken.None);

            if (job is null || !job.Statuses.All(s => s.Status != (short)JobStatus.Cancelled))
            {
                return;
            }

            await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Cancelled), CancellationToken.None);
            await _parcsDbContext.SaveChangesAsync(CancellationToken.None);

            await _jobTracker.CancelAndStopTrackingAsync(request.JobId);
        }
    }
}