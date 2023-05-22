using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Handlers
{
    public class GetModuleQueryHandler : IRequestHandler<GetModuleQuery, GetModuleQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;

        public GetModuleQueryHandler(ParcsDbContext parcsDbContext, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder)
        {
            _parcsDbContext = parcsDbContext;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
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

            var moduleDirectory = _moduleDirectoryPathBuilder.Build(module.Id);
            var moduleFiles = Directory.GetFiles(moduleDirectory).Select(Path.GetFileName).ToList();

            return new GetModuleQueryResponse
            {
                Id  = module.Id,
                Name = module.Name,
                CreateDateUtc = module.CreateDateUtc,
                Files = moduleFiles,
                Jobs = module.Jobs.Select(e => new GetJobQueryResponse
                {
                    Id = e.Id,
                    AssemblyName = e.AssemblyName,
                    ClassName = e.ClassName,
                    ModuleId = e.ModuleId,
                    ModuleName = e.Module.Name,
                    CreateDateUtc = e.CreateDateUtc,
                    Statuses = e.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                    Failures = e.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                }),
            };
        }
    }
}