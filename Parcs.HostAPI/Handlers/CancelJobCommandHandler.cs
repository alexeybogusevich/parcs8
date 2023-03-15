using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand>
    {
        private readonly IJobManager _jobManager;
        private readonly IFileManager _fileManager;

        public CancelJobCommandHandler(IJobManager jobManager, IFileManager fileManager)
        {
            _jobManager = jobManager;
            _fileManager = fileManager;
        }

        public Task Handle(CancelJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return Task.CompletedTask;
            }

            job.Cancel();
            _ = _jobManager.TryRemove(job.Id);

            return _fileManager.CleanAsync(job.Id, cancellationToken);
        }
    }
}