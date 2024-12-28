using MediatR;
using Parcs.Host.Models.Commands;
using Parcs.Host.Models.Responses;
using Parcs.Host.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Data.Entities;

namespace Parcs.Host.Handlers
{
    public sealed class CreateModuleCommandHandler(
        IFileSaver fileSaver,
        IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
        ParcsDbContext parcsDbContext) : IRequestHandler<CreateModuleCommand, CreateModuleCommandResponse>
    {
        private readonly IFileSaver _fileSaver = fileSaver;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;

        public async Task<CreateModuleCommandResponse> Handle(CreateModuleCommand request, CancellationToken cancellationToken)
        {
            var module = new ModuleEntity
            {
                Name = request.Name,
            };

            await _parcsDbContext.Modules.AddAsync(module, cancellationToken);
            await _parcsDbContext.SaveChangesAsync(cancellationToken);

            var moduleBinariesDirectoryPath = _moduleDirectoryPathBuilder.Build(module.Id);
            await _fileSaver.SaveAsync(request.BinaryFiles, moduleBinariesDirectoryPath, cancellationToken);

            return new CreateModuleCommandResponse(module.Id);
        }
    }
}