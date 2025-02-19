﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Handlers
{
    public class CreateJobFailureCommandHandler(ParcsDbContext parcsDbContext, IJobTracker jobTracker) : IRequestHandler<CreateJobFailureCommand>
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;
        private readonly IJobTracker _jobTracker = jobTracker;

        public async Task Handle(CreateJobFailureCommand request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs
                .Include(e => e.Statuses)
                .Include(e => e.Failures)
                .FirstOrDefaultAsync(e => e.Id == request.JobId, CancellationToken.None) ?? throw new ArgumentException("Job not found.");

            if (job.Statuses.All(s => s.Status != (short)JobStatus.Failed))
            {
                await _parcsDbContext.JobStatuses.AddAsync(new(job.Id, (short)JobStatus.Failed), CancellationToken.None);
            }

            await _parcsDbContext.JobFailures.AddAsync(new(job.Id, request.Message, request.StackTrace), CancellationToken.None);
            await _parcsDbContext.SaveChangesAsync(CancellationToken.None);

            await _jobTracker.CancelAndStopTrackingAsync(job.Id);
        }
    }
}