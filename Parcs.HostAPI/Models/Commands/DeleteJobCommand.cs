using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class DeleteJobCommand : IRequest
    {
        public DeleteJobCommand(Guid jobId)
        {
            JobId = jobId;
        }

        public Guid JobId { get; set; }
    }
}