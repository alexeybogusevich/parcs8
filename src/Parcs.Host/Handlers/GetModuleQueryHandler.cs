using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Handlers
{
    public class GetModuleQueryHandler : IRequestHandler<GetModuleQuery, GetModuleQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;

        public GetModuleQueryHandler(ParcsDbContext parcsDbContext)
        {
            _parcsDbContext = parcsDbContext;
        }

        public async Task<GetModuleQueryResponse> Handle(GetModuleQuery request, CancellationToken cancellationToken)
        {
            var module = await _parcsDbContext.Modules
                .Include(e => e.Jobs).ThenInclude(e => e.Statuses)
                .Include(e => e.Jobs).ThenInclude(e => e.Failures)
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            if (module is null)
            {
                return null;
            }

            return new GetModuleQueryResponse
            {
                Name = module.Name,
                Jobs = module.Jobs.Select(e => new GetJobQueryResponse
                {
                    ModuleId = e.ModuleId,
                    ModuleName = e.Module.Name,
                    Statuses = e.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                    Failures = e.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                }),
            };
        }
    }
}