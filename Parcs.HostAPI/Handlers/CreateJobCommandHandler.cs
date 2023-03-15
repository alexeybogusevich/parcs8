using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IJobManager _jobManager;
        private readonly IInputSaver _inputSaver;

        public CreateJobCommandHandler(IJobManager inputSaver, IInputSaver inputWriter)
        {
            _jobManager = inputSaver;
            _inputSaver = inputWriter;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create(request.ModuleId);
            await _inputSaver.SaveAsync(request.InputFiles, job.Id, job.CancellationToken);
            return new CreateJobCommandResponse(job.Id);
        }
    }
}