using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Responses.Nested;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Handlers
{
    public sealed class GetJobQueryHandler : IRequestHandler<GetJobQuery, GetJobQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;

        public GetJobQueryHandler(ParcsDbContext parcsDbContext)
        {
            _parcsDbContext = parcsDbContext;
        }

        public async Task<GetJobQueryResponse> Handle(GetJobQuery request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs
                .Include(e => e.Module)
                .Include(e => e.Statuses)
                .Include(e => e.Failures)
                .FirstOrDefaultAsync(e => e.Id == request.JobId, cancellationToken);

            if (job is null)
            {
                return null;
            }

            return new GetJobQueryResponse
            {
                ModuleId = job.ModuleId,
                ModuleName = job.Module.Name,
                Statuses = job.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                Failures = job.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
            };
        }
    }
}