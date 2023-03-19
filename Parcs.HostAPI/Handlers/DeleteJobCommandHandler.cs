using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class DeleteJobCommandHandler : IRequestHandler<DeleteJobCommand>
    {
        private readonly IJobManager _jobManager;

        public DeleteJobCommandHandler(IJobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
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