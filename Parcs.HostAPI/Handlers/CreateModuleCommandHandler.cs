using MediatR;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Handlers
{
    public class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, CreateModuleCommandResponse>
    {
        public Task<CreateModuleCommandResponse> Handle(CreateModuleCommand request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}