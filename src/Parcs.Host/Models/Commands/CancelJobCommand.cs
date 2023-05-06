using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class CancelJobCommand : IRequest
    {
        public long JobId { get; set; }
    }
}