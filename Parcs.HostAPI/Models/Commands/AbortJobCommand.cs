using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class AbortJobCommand : IRequest<bool>
    {
        public Guid JobId { get; set; }
    }
}