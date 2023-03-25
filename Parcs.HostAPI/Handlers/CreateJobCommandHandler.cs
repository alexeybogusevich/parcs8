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
        private readonly IJobDirectoryPathBuilder _jobDirectoryPathBuilder;
        private readonly IFileSaver _fileSaver;
        private readonly IMainModuleLoader _mainModuleLoader;

        public CreateJobCommandHandler(
            IJobManager jobManager,
            IJobDirectoryPathBuilder jobDirectoryPathBuilder,
            IFileSaver fileSaver,
            IMainModuleLoader mainModuleLoader)
        {
            _jobManager = jobManager;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileSaver = fileSaver;
            _mainModuleLoader = mainModuleLoader;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create(request.ModuleId, request.AssemblyName, request.ClassName);

            try
            {
                var mainModule = _mainModuleLoader.Load(request.ModuleId, request.AssemblyName, request.ClassName);
                job.SetMainModule(mainModule);
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