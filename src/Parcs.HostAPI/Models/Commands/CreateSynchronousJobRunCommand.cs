using MediatR;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateSynchronousJobRunCommand : CreateJobRunCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public CreateSynchronousJobRunCommand()
        {
        }

        public CreateSynchronousJobRunCommand(CreateJobRunCommand baseCommand)
            : base(baseCommand.ModuleId, baseCommand.MainModuleAssemblyName, baseCommand.MainModuleClassName, baseCommand.InputFiles, baseCommand.JsonArgumentsDictionary, baseCommand.NumberOfDaemons)
        {
        }
    }
}