using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateJobCommand : IRequest<CreateJobCommandResponse>
    {
        public Guid? ModuleId { get; set; }

        public IFormFile InputFile { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}