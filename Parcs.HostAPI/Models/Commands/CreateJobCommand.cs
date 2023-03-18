using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public Guid ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}