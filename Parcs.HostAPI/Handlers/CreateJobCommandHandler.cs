using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IJobManager _jobManager;
        private readonly IFileManager _fileManager;

        public CreateJobCommandHandler(IJobManager inputSaver, IFileManager fileManager)
        {
            _jobManager = inputSaver;
            _fileManager = fileManager;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create(request.ModuleId);
            await _fileManager.SaveAsync(request.InputFiles, DirectoryGroup.Input, job.Id, job.CancellationToken);
            return new CreateJobCommandResponse(job.Id);
        }
    }
}