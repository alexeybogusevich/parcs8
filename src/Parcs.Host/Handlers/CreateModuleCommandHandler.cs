using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;
using Parcs.Data.Entities;

namespace Parcs.HostAPI.Handlers
{
    public sealed class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, CreateModuleCommandResponse>
    {
        private readonly IFileSaver _fileSaver;
        private readonly IModuleDirectoryPathBuilder _moduleDirectoryPathBuilder;
        private readonly ParcsDbContext _parcsDbContext;

        public CreateModuleCommandHandler(
            IFileSaver fileSaver,
            IModuleDirectoryPathBuilder moduleDirectoryPathBuilder,
            ParcsDbContext parcsDbContext)
        {
            _fileSaver = fileSaver;
            _moduleDirectoryPathBuilder = moduleDirectoryPathBuilder;
            _parcsDbContext = parcsDbContext;
        }

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