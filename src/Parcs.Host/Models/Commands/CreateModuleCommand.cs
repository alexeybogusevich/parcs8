using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class CreateModuleCommand : IRequest<CreateModuleCommandResponse>
    {
        public string Name { get; set; }

        public IEnumerable<IFormFile> BinaryFiles { get; set; }
    }
}