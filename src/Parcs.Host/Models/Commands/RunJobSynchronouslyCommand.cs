using MediatR;
using Parcs.Host.Models.Commands.Base;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class RunJobSynchronouslyCommand : RunJobCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public RunJobSynchronouslyCommand()
        {
        }

        public RunJobSynchronouslyCommand(RunJobCommand baseCommand)
            : base(baseCommand.JobId, baseCommand.PointsNumber, baseCommand.RawArgumentsDictionary)
        {
        }
    }
}