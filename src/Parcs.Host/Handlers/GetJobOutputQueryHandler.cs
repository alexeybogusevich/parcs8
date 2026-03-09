using MediatR;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Parcs.Host.Configuration;

namespace Parcs.Host.Handlers
{
    public sealed class GetJobOutputQueryHandler(
        ParcsDbContext parcsDbContext,
        IJobDirectoryPathBuilder jobDirectoryPathBuilder,
        IFileArchiver fileArchiver,
        IOptions<JobOutputConfiguration> options) : IRequestHandler<GetJobOutputQuery, GetJobOutputQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
        private readonly IFileArchiver _fileArchiver = fileArchiver;
        private readonly JobOutputConfiguration _configuration = options.Value;

        public async Task<GetJobOutputQueryResponse> Handle(GetJobOutputQuery request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs.FirstOrDefaultAsync(e => e.Id == request.JobId, cancellationToken);

            if (job is null)
            {
                return null;
            }

            var outputDirectoryPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output);

            // Build a human-readable archive name that avoids browser "JobOutput1(1).zip" collisions.
            // ClassName is e.g. "IslandModelWithMigrationMainModule" → strip suffix → "island-model-with-migration"
            var moduleSlug = BuildModuleSlug(job.ClassName);
            var datePart   = job.CreateDateUtc.ToString("yyyyMMdd-HHmm");
            var outputArchiveName = $"parcs-job{job.Id}-{moduleSlug}-{datePart}";

            var outputArchive = await _fileArchiver.ArchiveDirectoryAsync(outputDirectoryPath, outputArchiveName, cancellationToken);

            return new GetJobOutputQueryResponse(outputArchive);
        }

        /// <summary>
        /// Converts a MainModule class name to a short kebab-case slug.
        /// e.g. "IslandModelWithMigrationMainModule" → "island-model-with-migration"
        ///      "ParallelMainModule"                 → "parallel"
        ///      null / unknown                       → "module"
        /// </summary>
        private static string BuildModuleSlug(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return "module";

            // className may be fully-qualified (e.g. "Parcs.Modules.TravelingSalesman.Parallel.IslandModelWithMigrationMainModule")
            // — take only the simple class name after the last dot.
            var simpleName = className.Contains('.')
                ? className.Split('.').Last()
                : className;

            // Strip "MainModule" suffix if present
            var name = simpleName.EndsWith("MainModule", StringComparison.OrdinalIgnoreCase)
                ? simpleName[..^"MainModule".Length]
                : simpleName;

            // Insert hyphen before each uppercase letter that follows a lowercase letter
            // e.g. "IslandModelWithMigration" → "Island-Model-With-Migration"
            var hyphenated = System.Text.RegularExpressions.Regex.Replace(
                name,
                @"(?<=[a-z])(?=[A-Z])",
                "-");

            return hyphenated.ToLowerInvariant();
        }
    }
}