using MediatR;
using Parcs.Host.Models.Commands.Base;

namespace Parcs.Host.Models.Commands
{
    public class RunJobAsynchronouslyCommand : RunJobCommand, IRequest
    {
        public RunJobAsynchronouslyCommand()
        {
        }

        public RunJobAsynchronouslyCommand(RunJobCommand baseCommand, string callbackUrl)
            : base(baseCommand.JobId, baseCommand.PointsNumber, baseCommand.Arguments)
        {
            CallbackUrl = callbackUrl;
        }

        public string CallbackUrl { get; set; }
    }
}