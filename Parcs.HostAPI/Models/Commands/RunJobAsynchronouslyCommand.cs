using MediatR;
using Parcs.HostAPI.Models.Commands.Base;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobAsynchronouslyCommand : RunJobCommand, IRequest
    {
        public RunJobAsynchronouslyCommand(RunJobCommand command, string callbackUrl)
        {
            JobId = command.JobId;
            Daemons = command.Daemons;
            CallbackUrl = callbackUrl;
        }

        public string CallbackUrl { get; set; }
    }
}