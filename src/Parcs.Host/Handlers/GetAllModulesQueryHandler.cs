using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Handlers
{
    public class GetAllModulesQueryHandler(ParcsDbContext parcsDbContext) : IRequestHandler<GetAllModulesQuery, IEnumerable<GetPlainModuleQueryResponse>>
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;

        public async Task<IEnumerable<GetPlainModuleQueryResponse>> Handle(GetAllModulesQuery request, CancellationToken cancellationToken)
        {
            return await _parcsDbContext.Modules
                .Select(e => new GetPlainModuleQueryResponse
                {
                    Id = e.Id,
                    Name = e.Name,
                    CreateDateUtc = e.CreateDateUtc,
                    JobsNumber = e.Jobs.Count,
                })
                .OrderByDescending(e => e.Id)
                .ToListAsync(cancellationToken);
        }
    }
}