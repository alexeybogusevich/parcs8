using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateModuleCommand : IRequest<CreateModuleCommandResponse>
    {
        public string MainModuleName { get; set; }

        public IFormFile MainModuleCompiled { get; set; }

        public string WorkerModuleName { get; set; }

        public IFormFile WorkerModuleCompiled { get; set; }
    }
}