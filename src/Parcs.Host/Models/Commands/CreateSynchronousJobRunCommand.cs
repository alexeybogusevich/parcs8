using MediatR;
using Parcs.Host.Models.Commands.Base;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Commands
{
    public class CreateSynchronousJobRunCommand : CreateJobRunCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public CreateSynchronousJobRunCommand()
        {
        }

        public CreateSynchronousJobRunCommand(CreateJobRunCommand baseCommand)
            : base(baseCommand.ModuleId, baseCommand.MainModuleAssemblyName, baseCommand.MainModuleClassName, baseCommand.InputFiles, baseCommand.PointsNumber, baseCommand.RawArgumentsDictionary)
        {
        }
    }
}