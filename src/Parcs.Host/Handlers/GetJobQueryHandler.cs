using MediatR;
using Parcs.HostAPI.Models.Queries;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public sealed class GetJobQueryHandler : IRequestHandler<GetJobQuery, GetJobQueryResponse>
    {
        private readonly IJobManager _jobManager;

        public GetJobQueryHandler(IJobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public Task<GetJobQueryResponse> Handle(GetJobQuery request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                throw new ArgumentException($"Job not found: {request.JobId}.");
            }

            return Task.FromResult(new GetJobQueryResponse
            {
                JobStatus = job.Status,
                CreateDateUtc = job.CreateDateUtc,
                StartDateUtc = job.StartDateUtc,
                EndDateUtc = job.EndDateUtc,
                ErrorMessage = job.ErrorMessage,
            });
        }
    }
}