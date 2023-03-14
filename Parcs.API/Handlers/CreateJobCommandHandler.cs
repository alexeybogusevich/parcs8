using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IJobManager _jobManager;
        private readonly IInputWriter _inputWriter;

        public CreateJobCommandHandler(IJobManager jobManager, IInputWriter inputWriter)
        {
            _jobManager = jobManager;
            _inputWriter = inputWriter;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create();
            job.SetModule(request.ModuleId);
            await _inputWriter.WriteAllAsync(request.InputFiles, job.Id, job.CancellationToken);
            return new CreateJobCommandResponse(job.Id);
        }
    }
}