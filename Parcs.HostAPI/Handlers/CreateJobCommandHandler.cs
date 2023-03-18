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

        public CreateJobCommandHandler(IJobManager jobManager, IJobDirectoryPathBuilder jobDirectoryPathBuilder, IFileSaver fileSaver)
        {
            _jobManager = jobManager;
            _jobDirectoryPathBuilder = jobDirectoryPathBuilder;
            _fileSaver = fileSaver;
        }

        public async Task<CreateJobCommandResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
        {
            var job = _jobManager.Create(request.ModuleId);

            try
            {
                var mainModule = await _mainModuleLoader.LoadAsync(request.ModuleId, request.AssemblyName, request.ClassName, job.CancellationToken);
                job.SetMainModule(mainModule);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                job.Fail(ex.Message);
            }

            var inputPath = _jobDirectoryPathBuilder.Build(job.Id, JobDirectoryGroup.Input);
            await _fileSaver.SaveAsync(request.InputFiles, inputPath, job.CancellationToken);

            return new CreateJobCommandResponse(job);
        }
    }
}