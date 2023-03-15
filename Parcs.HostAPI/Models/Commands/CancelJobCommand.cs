using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class CancelJobCommand : IRequest<bool>
    {
        public Guid JobId { get; set; }
    }
}