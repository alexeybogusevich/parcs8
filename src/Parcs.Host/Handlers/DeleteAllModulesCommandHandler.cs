using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;

namespace Parcs.HostAPI.Handlers
{
    public sealed class DeleteAllModulesCommandHandler : IRequestHandler<DeleteAllModulesCommand>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public DeleteAllModulesCommandHandler(
            ParcsDbContext parcsDbContext, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IFileEraser fileEraser)
        {
            _parcsDbContext = parcsDbContext;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public async Task Handle(DeleteAllModulesCommand request, CancellationToken cancellationToken)
        {
            _parcsDbContext.Modules.RemoveRange(_parcsDbContext.Modules);

            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            _fileEraser.TryDeleteRecursively(_moduleDirectoryPathBuilder.Build());
        }
    }
}