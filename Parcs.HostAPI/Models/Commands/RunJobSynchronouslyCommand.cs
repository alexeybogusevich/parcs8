using MediatR;
using Parcs.Core;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobSynchronouslyCommand : IRequest<RunJobSynchronouslyCommandResponse>
    {
        public RunJobSynchronouslyCommand()
        {
        }

        public RunJobSynchronouslyCommand(CreateSynchronousJobRunCommand command)
        {
            JobId = command.JobId;
            Daemons = command.Daemons;
        }

        public RunJobSynchronouslyCommand(RunJobAsynchronouslyCommand command)
        {
            JobId = command.JobId;
            Daemons = command.Daemons;
        }

        public Guid JobId { get; set; }

        public IEnumerable<Daemon> Daemons { get; set; }
    }
}