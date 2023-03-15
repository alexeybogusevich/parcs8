using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class CancelJobCommand : IRequest
    {
        public Guid JobId { get; set; }
    }
}