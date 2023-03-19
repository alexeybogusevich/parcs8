using MediatR;
using Parcs.Core;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobAsynchronouslyCommand : IRequest
    {
        public RunJobAsynchronouslyCommand(CreateAsynchronousJobRunCommand command)
        {
            JobId = command.JobId;
            Daemons = command.Daemons;
            CallbackUrl = command.CallbackUrl;
        }

        public Guid JobId { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }

        public string CallbackUrl { get; set; }
    }
}