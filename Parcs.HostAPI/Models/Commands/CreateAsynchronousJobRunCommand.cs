using MediatR;
using Parcs.HostAPI.Models.Commands.Base;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateAsynchronousJobRunCommand : CreateJobRunCommand, IRequest
    {
        public CreateAsynchronousJobRunCommand(CreateJobRunCommand baseCommand, string callbackUrl)
        {
            ModuleId = baseCommand.ModuleId;
            AssemblyName = baseCommand.AssemblyName;
            ClassName = baseCommand.ClassName;
            InputFiles = baseCommand.InputFiles;
            Daemons = baseCommand.Daemons;
            CallbackUrl = callbackUrl;
        }

        public string CallbackUrl { get; set; }
    }
}