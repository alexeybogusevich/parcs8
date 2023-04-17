using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Models.Enums;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public sealed class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, CreateJobCommandResponse>
    {
        private readonly IJobManager _jobManager;
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileSaver _fileSaver;
        private readonly IModuleLoader _moduleLoader;

        public CreateJobCommandHandler(
            IJobManager jobManager,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileSaver fileSaver,
            IModuleLoader mainModuleLoader)
        {
            _jobManager = jobManager;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileSaver = fileSaver;
            _moduleLoader = mainModuleLoader;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create(request.ModuleId, request.AssemblyName, request.ClassName);

            try
            {
                var module = _moduleLoader.Load(request.ModuleId, request.AssemblyName, request.ClassName);
                job.SetModule(module);
            }
            catch
            {
                _ = _jobManager.TryRemove(job.Id);
                throw;
            }

            var inputPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Input);
            await _fileSaver.SaveAsync(request.InputFiles, inputPath, job.CancellationToken);

            return new CreateJobCommandResponse(job);
        }
    }
}