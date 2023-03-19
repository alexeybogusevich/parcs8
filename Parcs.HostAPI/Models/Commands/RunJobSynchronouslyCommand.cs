using MediatR;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobSynchronouslyCommand : RunJobCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public RunJobSynchronouslyCommand(RunJobCommand command)
        {
            JobId = command.JobId;
            Daemons = command.Daemons; 
        }
    }
}