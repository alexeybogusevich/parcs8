using MediatR;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobSynchronouslyCommand : RunJobCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public RunJobSynchronouslyCommand()
        {
        }

        public RunJobSynchronouslyCommand(RunJobCommand baseCommand)
            : base(baseCommand.JobId, baseCommand.Daemons)
        {
            JobId = baseCommand.JobId;
            Daemons = baseCommand.Daemons; 
        }
    }
}