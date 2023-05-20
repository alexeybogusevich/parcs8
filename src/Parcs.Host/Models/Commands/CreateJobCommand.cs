using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public long ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}