using MediatR;
using Microsoft.Extensions.Options;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Models.Queries;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using System.Text;

namespace Parcs.HostAPI.Handlers
{
    public sealed class GetJobOutputQueryHandler : IRequestHandler<GetJobOutputQuery, GetJobOutputQueryResponse>
    {
        private readonly IJobManager _jobManager;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IInputOutputFactory _inputOutputFactory;
        private readonly IFileArchiver _fileArchiver;

        private readonly JobOutputConfiguration _configuration;

        public GetJobOutputQueryHandler(
            IJobManager jobManager,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IInputOutputFactory inputOutputFactory,
            IFileArchiver fileArchiver,
            IOptions<JobOutputConfiguration> options)
        {
            _jobManager = jobManager;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _inputOutputFactory = inputOutputFactory;
            _fileArchiver = fileArchiver;
            _configuration = options.Value;
        }

        public async Task<GetJobOutputQueryResponse> Handle(GetJobOutputQuery request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {request.JobId}");
            }

            var jobSummary = job.ToString();
            var jobSummaryBytes = Encoding.UTF8.GetBytes(jobSummary);

            await _inputOutputFactory.CreateWriter(job).WriteToFileAsync(jobSummaryBytes, _configuration.JobInfoFilename);

            var outputDirectoryPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Output);
            var outputDirectoryArchive = await _fileArchiver.ArchiveDirectoryAsync(outputDirectoryPath, cancellationToken);

            return new GetJobOutputQueryResponse(outputDirectoryArchive);
        }
    }
}