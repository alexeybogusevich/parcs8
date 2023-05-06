using MediatR;
using Parcs.Host.Models.Queries;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Parcs.Host.Handlers
{
    public sealed class GetJobOutputQueryHandler : IRequestHandler<GetJobOutputQuery, GetJobOutputQueryResponse>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileArchiver _fileArchiver;

        public GetJobOutputQueryHandler(
            ParcsDbContext parcsDbContext, IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileArchiver fileArchiver)
        {
            _parcsDbContext = parcsDbContext;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileArchiver = fileArchiver;
        }

        public async Task<GetJobOutputQueryResponse> Handle(GetJobOutputQuery request, CancellationToken cancellationToken)
        {
            var job = await _parcsDbContext.Jobs.FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return null;
            }
            
            var outputDirectoryPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output);
            var outputDirectoryArchive = await _fileArchiver.ArchiveDirectoryAsync(outputDirectoryPath, cancellationToken);

            return new GetJobOutputQueryResponse(outputDirectoryArchive);
        }
    }
}