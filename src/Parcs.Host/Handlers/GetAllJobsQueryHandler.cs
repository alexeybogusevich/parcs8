using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses.Nested;
using Parcs.Host.Models.Responses;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Models.Enums;

namespace Parcs.Host.Handlers
{
    public class GetAllJobsQueryHandler : IRequestHandler<GetAllJobsQuery, IEnumerable<GetJobQueryResponse>>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;

        public GetAllJobsQueryHandler(ParcsDbContext parcsDbContext, IJobDirectoryPathBuilder jobDirectoryPathBuilder)
        {
            _parcsDbContext = parcsDbContext;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        }

        public async Task<IEnumerable<GetJobQueryResponse>> Handle(GetAllJobsQuery request, CancellationToken cancellationToken)
        {
            var jobs = await _parcsDbContext.Jobs.Select(
                e => new GetJobQueryResponse
                {
                    Id = e.Id,
                    AssemblyName = e.AssemblyName,
                    ClassName = e.ClassName,
                    ModuleId = e.ModuleId,
                    ModuleName = e.Module.Name,
                    CreateDateUtc = e.CreateDateUtc,
                    Statuses = e.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                    Failures = e.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                })
                .OrderByDescending(e => e.Id)
                .ToListAsync(cancellationToken);

            foreach (var job in jobs)
            {
                job.HasOutput = Directory.Exists(_jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output));
            }

            return jobs;
        }
    }
}