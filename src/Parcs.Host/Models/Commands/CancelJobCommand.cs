using MediatR;

namespace Parcs.Host.Models.Commands
{
    public class CancelJobCommand : IRequest
    {
        public long JobId { get; set; }
    }
}