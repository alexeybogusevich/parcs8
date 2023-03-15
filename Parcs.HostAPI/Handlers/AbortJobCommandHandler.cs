using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class AbortJobCommandHandler : IRequestHandler<AbortJobCommand, bool>
    {
        private readonly IJobManager _jobManager;

        public AbortJobCommandHandler(IJobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public async Task<bool> Handle(AbortJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return false;
            }

            job.Abort();
            _ = _jobManager.TryRemove(job.Id);

            await Task.CompletedTask;

            return true;
        }
    }
}