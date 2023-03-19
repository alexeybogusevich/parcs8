using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateSynchronousJobRunCommand : IRequest<RunJobSynchronouslyCommandResponse>
    {
        public Guid JobId { get; set; }

        public Guid ModuleId { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}