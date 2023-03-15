using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class DeleteJobCommand : IRequest
    {
        public Guid JobId { get; set; }
    }
}