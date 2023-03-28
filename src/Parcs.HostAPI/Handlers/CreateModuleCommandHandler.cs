using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Enums;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, CreateModuleCommandResponse>
    {
        private readonly IFileSaver _fileSaver;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IGuidReference _guidReference;

        public CreateModuleCommandHandler(
            IFileSaver fileSaver,
            IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
            IGuidReference guidReference)
        {
            _fileSaver = fileSaver;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _guidReference = guidReference;
        }

        public async Task<CreateModuleCommandResponse> Handle(CreateModuleCommand request, CancellationToken cancellationToken)
        {
            var moduleId = _guidReference.NewGuid();

            var mainModulesDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Main);
            var workerModulesDirectoryPath = _moduleDirectoryPathBuilder.Build(moduleId, ModuleDirectoryGroup.Worker);

            await _fileSaver.SaveAsync(request.MainModuleAssemblyFiles, mainModulesDirectoryPath, cancellationToken);
            await _fileSaver.SaveAsync(request.WorkerModuleAssemblyFiles, workerModulesDirectoryPath, cancellationToken);

            return new CreateModuleCommandResponse(moduleId);
        }
    }
}