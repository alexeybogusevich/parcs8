using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Models;
using Parcs.Data.Context;
using Parcs.Host.Models.Responses.Nested;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Host.Services.Interfaces;
using Parcs.Net;

namespace Parcs.Host.Handlers
{
    public sealed class GetJobQueryHandler(
        ParcsDbContext parcsDbContext,
        IJobDirectoryPathBuilder jobDirectoryPathBuilder,
        IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
        IMetadataLoadContextProvider metadataLoadContextProvider) : IRequestHandler<GetJobQuery, GetJobQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        private readonly IMetadataLoadContextProvider _metadataLoadContextProvider = metadataLoadContextProvider;

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

            var moduleFiles = Directory.GetFiles(_moduleDirectoryPathBuilder.Build(job.ModuleId));
            var moduleOptions = new List<string>();

            foreach (var assemblyPath in moduleFiles.Where(f => Path.GetFileName(f).Contains(".dll")).ToList())
            {
                using var assemblyMetadataContext = _metadataLoadContextProvider.Get(assemblyPath, typeof(IModule).Assembly.Location);

                var assembly = assemblyMetadataContext.LoadFromAssemblyPath(assemblyPath);
                var assemblyModuleOptions = assembly
                    .GetTypes()
                    .Where(t => t.GetInterface(nameof(IModuleOptions)) is not null)
                    .SelectMany(t => t.GetProperties())
                    .Select(p => p.Name)
                    .ToArray();

                moduleOptions.AddRange(assemblyModuleOptions);
            }

            return new GetJobQueryResponse
            {
                Id = job.Id,
                ClassName = job.ClassName,
                AssemblyName = job.AssemblyName,
                ModuleId = job.ModuleId,
                ModuleName = job.Module.Name,
                CreateDateUtc = job.CreateDateUtc,
                HasOutput = Directory.Exists(_jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output)),
                Statuses = job.Statuses.Select(s => new JobStatusResponse((JobStatus)s.Status, s.CreateDateUtc)).ToList(),
                Failures = job.Failures.Select(f => new JobFailureResponse(f.Message, f.StackTrace, f.CreateDateUtc)).ToList(),
                Options = moduleOptions,
            };
        }
    }
}