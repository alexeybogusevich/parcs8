using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Handlers
{
    public class GetAllJobsQueryHandler : IRequestHandler<GetAllJobsQuery, IEnumerable<GetPlainJobQueryResponse>>
    {
        private readonly ParcsDbContext _parcsDbContext;

        public GetAllJobsQueryHandler(ParcsDbContext parcsDbContext)
        {
            _parcsDbContext = parcsDbContext;
        }

        public async Task<IEnumerable<GetPlainJobQueryResponse>> Handle(GetAllJobsQuery request, CancellationToken cancellationToken)
        {
            return await _parcsDbContext.Jobs.Select(
                e => new GetPlainJobQueryResponse
                {
                    Id = e.Id,
                    ModuleId = e.ModuleId,
                    ModuleName = e.Module.Name,
                    AssemblyName = e.AssemblyName,
                    ClassName = e.ClassName,
                    CreateDateUtc = e.CreateDateUtc,
                    Status = (JobStatus)e.Statuses.OrderByDescending(s => s.Id).FirstOrDefault().Status,
                })
                .OrderByDescending(e => e.Id)
                .ToListAsync(cancellationToken);
        }
    }
}