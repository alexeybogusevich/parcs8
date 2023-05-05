using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand>
    {
        private readonly IJobManager _jobManager;

        public CancelJobCommandHandler(IJobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public Task Handle(CancelJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return Task.CompletedTask;
            }

            job.Cancel();

            return Task.CompletedTask;
        }
    }
}