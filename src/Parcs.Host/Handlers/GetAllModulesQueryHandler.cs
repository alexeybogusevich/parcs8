using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Handlers
{
    public class GetAllModulesQueryHandler : IRequestHandler<GetAllModulesQuery, IEnumerable<GetModuleQueryResponse>>
    {
        private readonly ParcsDbContext _parcsDbContext;

        public GetAllModulesQueryHandler(ParcsDbContext parcsDbContext)
        {
            _parcsDbContext = parcsDbContext;
        }

        public async Task<IEnumerable<GetModuleQueryResponse>> Handle(GetAllModulesQuery request, CancellationToken cancellationToken)
        {
            return await _parcsDbContext.Modules
                .Select(e => new GetModuleQueryResponse
                {
                    Id = e.Id,
                    Name = e.Name,
                    Jobs = e.Jobs.Select(j => new JobResponse
                    {
                        JobId = j.Id,
                        Statuses = j.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                        Failures = j.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                    })
                })
                .ToListAsync(cancellationToken);
        }
    }
}