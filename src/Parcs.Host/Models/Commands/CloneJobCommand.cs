using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class CloneJobCommand : IRequest<CloneJobCommandResponse>
    {
        public long JobId { get; set; }
    }
}