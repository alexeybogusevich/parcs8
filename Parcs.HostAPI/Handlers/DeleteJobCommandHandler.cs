using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class DeleteJobCommandHandler : IRequestHandler<DeleteJobCommand>
    {
        private readonly IJobManager _jobManager;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public DeleteJobCommandHandler(IJobManager jobManager, IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileEraser fileEraser)
        {
            _jobManager = jobManager;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
        {
            if (!_jobManager.TryGet(request.JobId, out var job))
            {
                return Task.CompletedTask;
            }

            job.Cancel();

            var jobDirectoryPath = _jobDirectoryPathBuilder.Build(job.Id);
            _fileEraser.TryDeleteRecursively(jobDirectoryPath);

            _ = _jobManager.TryRemove(job.Id);

            return Task.CompletedTask;
        }
    }
}