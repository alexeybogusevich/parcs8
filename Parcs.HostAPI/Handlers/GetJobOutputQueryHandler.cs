using MediatR;
using Parcs.HostAPI.Models.Queries;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class GetJobOutputQueryHandler : IRequestHandler<GetJobOutputQuery, GetJobOutputQueryResponse>
    {
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileReader _fileReader;

        public GetJobOutputQueryHandler(IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileReader fileReader)
        {
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileReader = fileReader;
        }

        public async Task<GetJobOutputQueryResponse> Handle(GetJobOutputQuery request, CancellationToken cancellationToken)
        {
            var jobOutputDirectoryPath = _jobDirectoryPathBuilder.Build(request.JobId);
            var fileDescriptions = await _fileReader.ReadAsync(jobOutputDirectoryPath, cancellationToken);
            return new GetJobOutputQueryResponse { FileDescriptions = fileDescriptions };
        }
    }
}