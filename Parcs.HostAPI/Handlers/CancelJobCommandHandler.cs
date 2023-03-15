using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand, bool>
    {
        private readonly IJobManager _jobManager;

        public CancelJobCommandHandler(IJobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public async Task<bool> Handle(CancelJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return false;
            }

            job.Cancel();
            _ = _jobManager.TryRemove(job.Id);

            await Task.CompletedTask;

            return true;
        }
    }
}