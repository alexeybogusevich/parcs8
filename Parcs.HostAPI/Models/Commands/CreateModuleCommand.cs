using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateModuleCommand : IRequest<CreateModuleCommandResponse>
    {
        public IFormFile MainModuleAssembly { get; set; }

        public IEnumerable<IFormFile> WorkerModuleAssemblies { get; set; }
    }
}