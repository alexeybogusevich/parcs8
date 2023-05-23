using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Models.Responses.Nested;
using Parcs.Host.Services.Interfaces;
using Parcs.Net;
using System.Reflection;

namespace Parcs.Host.Handlers
{
    public class GetModuleQueryHandler : IRequestHandler<GetModuleQuery, GetModuleQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IMetadataLoadContextProvider _metadataLoadContextProvider;

        public GetModuleQueryHandler(
            ParcsDbContext parcsDbContext, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IMetadataLoadContextProvider metadataLoadContextProvider)
        {
            _parcsDbContext = parcsDbContext;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _metadataLoadContextProvider = metadataLoadContextProvider;
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
            var moduleFiles = Directory.GetFiles(moduleDirectory);
            var moduleAssemblies = new List<AssemblyMetadataResponse>();

            foreach (var assemblyPath in moduleFiles.Where(f => Path.GetFileName(f).Contains(".dll")).ToList())
            {
                using var assemblyMetadataContext = _metadataLoadContextProvider.Get(assemblyPath, typeof(IModule).Assembly.Location);

                var assembly = assemblyMetadataContext.LoadFromAssemblyPath(assemblyPath);
                var assemblyModules = assembly
                    .GetTypes()
                    .Where(t => t.GetInterface(nameof(IModule)) is not null)
                    .Select(t => t.FullName)
                    .ToList();

                moduleAssemblies.Add(new AssemblyMetadataResponse(assembly.GetName().Name, assemblyModules));
            }

            return new GetModuleQueryResponse
            {
                Id  = module.Id,
                Name = module.Name,
                CreateDateUtc = module.CreateDateUtc,
                Files = moduleFiles.Select(Path.GetFileName).ToList(),
                Assemblies = moduleAssemblies,
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