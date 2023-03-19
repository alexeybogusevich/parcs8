using MediatR;
using Parcs.HostAPI.Models.Commands.Base;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateSynchronousJobRunCommand : CreateJobRunCommand, IRequest<RunJobSynchronouslyCommandResponse>
    {
        public CreateSynchronousJobRunCommand(CreateJobRunCommand baseCommand)
        {
            ModuleId = baseCommand.ModuleId;
            AssemblyName = baseCommand.AssemblyName;
            ClassName = baseCommand.ClassName;
            InputFiles = baseCommand.InputFiles;
            Daemons = baseCommand.Daemons;
        }
    }
}