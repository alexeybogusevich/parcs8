using MediatR;
using Parcs.Core;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateAsynchronousJobRunCommand : IRequest
    {
        public Guid JobId { get; set; }

        public Guid ModuleId { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }

        public string CallbackUrl { get; set; }
    }
}