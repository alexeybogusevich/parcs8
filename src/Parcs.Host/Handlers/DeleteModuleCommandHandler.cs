using MediatR;
using Microsoft.EntityFrameworkCore;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Host.Models.Commands;
using Parcs.Host.Services.Interfaces;

namespace Parcs.Host.Handlers
{
    public class DeleteModuleCommandHandler : IRequestHandler<DeleteModuleCommand>
    {
        private readonly ParcsDbContext _parcsDbContext;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly IFileEraser _fileEraser;

        public DeleteModuleCommandHandler(
            ParcsDbContext parcsDbContext, IModuleDirectoryPathBuilder moduleDirectoryPathBuilder, IFileEraser fileEraser)
        {
            _parcsDbContext = parcsDbContext;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _fileEraser = fileEraser;
        }

        public async Task Handle(DeleteModuleCommand request, CancellationToken cancellationToken)
        {
            var module = await _parcsDbContext.Modules.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (module is null)
            {
                return;
            }

            _parcsDbContext.Modules.Remove(module);

            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            _fileEraser.TryDeleteRecursively(_moduleDirectoryPathBuilder.Build(request.Id));
        }
    }
}