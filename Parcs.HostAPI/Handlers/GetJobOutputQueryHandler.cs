using MediatR;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Models.Queries;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class GetJobOutputQueryHandler : IRequestHandler<GetJobOutputQuery, GetJobOutputQueryResponse>
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileArchiver _fileArchiver;

        public GetJobOutputQueryHandler(IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileArchiver fileArchiver)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileArchiver = fileArchiver;
        }

        public async Task<GetJobOutputQueryResponse> Handle(GetJobOutputQuery request, CancellationToken cancellationToken)
        {
            var jobOutputDirectoryPath = _jobDirectoryPathBuilder.Build(request.JobId, JobDirectoryGroup.Output);
            var archivedOutput = await _fileArchiver.ArchiveDirectoryAsync(jobOutputDirectoryPath, cancellationToken);
            return new GetJobOutputQueryResponse(archivedOutput);
        }
    }
}