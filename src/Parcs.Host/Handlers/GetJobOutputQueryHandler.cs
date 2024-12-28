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

            var outputArchiveName = string.Format(_configuration.Filename, job.Id);
            var outputArchive = await _fileArchiver.ArchiveDirectoryAsync(outputDirectoryPath, outputArchiveName, cancellationToken);

            return new GetJobOutputQueryResponse(outputArchive);
        }
    }
}