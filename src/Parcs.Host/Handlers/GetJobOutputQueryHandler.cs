using MediatR;
using Parcs.HostAPI.Models.Queries;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Models.Enums;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Parcs.HostAPI.Handlers
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
            var job = await _parcsDbContext.Jobs.FirstOrDefaultAsync(cancellationToken) 
                ?? throw new ArgumentException($"Job not found: {request.JobId}");
            
            var outputDirectoryPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output);
            var outputDirectoryArchive = await _fileArchiver.ArchiveDirectoryAsync(outputDirectoryPath, cancellationToken);

            return new GetJobOutputQueryResponse(outputDirectoryArchive);
        }
    }
}