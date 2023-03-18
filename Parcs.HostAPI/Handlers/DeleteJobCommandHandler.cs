using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class DeleteJobCommandHandler : IRequestHandler<DeleteJobCommand>
    {
        private readonly IJobManager _jobManager;
        private readonly IFileSaver _fileManager;

        public DeleteJobCommandHandler(IJobManager jobManager, IFileSaver fileManager)
        {
            _jobManager = jobManager;
            _fileManager = fileManager;
        }

        public Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return Task.CompletedTask;
            }

            job.Cancel();
            _ = _jobManager.TryRemove(job.Id);

            //var fileDeletionTasks = new Task[]
            //{
            //    _fileManager.DeleteAsync(JobDirectoryGroup.Input, job.Id, cancellationToken),
            //    _fileManager.DeleteAsync(JobDirectoryGroup.Output, job.Id, cancellationToken),
            //};

            return Task.WhenAll(fileDeletionTasks);
        }
    }
}