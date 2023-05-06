using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses.Nested;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Handlers
{
    public class GetAllJobsQueryHandler : IRequestHandler<GetAllJobsQuery, IEnumerable<GetJobQueryResponse>>
    {
        private readonly ParcsDbContext _parcsDbContext;

        public GetAllJobsQueryHandler(ParcsDbContext parcsDbContext)
        {
            _parcsDbContext = parcsDbContext;
        }

        public async Task<IEnumerable<GetJobQueryResponse>> Handle(GetAllJobsQuery request, CancellationToken cancellationToken)
        {
            return await _parcsDbContext.Jobs.Select(
                e => new GetJobQueryResponse
                {
                    JobId = e.Id,
                    ModuleId = e.ModuleId,
                    ModuleName = e.Module.Name,
                    Statuses = e.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                    Failures = e.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                })
                .ToListAsync(cancellationToken);
        }
    }
}