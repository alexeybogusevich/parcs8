using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobCommand : IRequest<RunJobCommandResponse>
    {
        public Guid? ModuleId { get; set; }

        public IEnumerable<IFormFile> InputFiles { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}