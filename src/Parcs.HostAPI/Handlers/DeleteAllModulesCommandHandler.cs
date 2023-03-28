using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;

namespace Parcs.HostAPI.Handlers
{
    public class DeleteAllModulesCommandHandler : IRequestHandler<DeleteAllModulesCommand>
    {
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public DeleteAllModulesCommandHandler(IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IFileEraser fileEraser)
        {
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public Task Handle(DeleteAllModulesCommand request, CancellationToken cancellationToken)
        {
            var modulesDirectoryPath = _moduleDirectoryPathBuilder.Build();
            _fileEraser.TryDeleteRecursively(modulesDirectoryPath);
            return Task.CompletedTask;
        }
    }
}