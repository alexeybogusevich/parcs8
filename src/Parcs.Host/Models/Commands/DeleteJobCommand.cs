using MediatR;

namespace Parcs.HostAPI.Models.Commands
{
    public class DeleteJobCommand : IRequest
    {
        public DeleteJobCommand()
        {
        }

        public DeleteJobCommand(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}