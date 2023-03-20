using MediatR;
using Parcs.HostAPI.Models.Commands.Base;

namespace Parcs.HostAPI.Models.Commands
{
    public class CreateAsynchronousJobRunCommand : CreateJobRunCommand, IRequest
    {
        public CreateAsynchronousJobRunCommand()
        {
        }

        public CreateAsynchronousJobRunCommand(CreateJobRunCommand baseCommand, string callbackUrl)
            : base(baseCommand.ModuleId, baseCommand.AssemblyName, baseCommand.ClassName, baseCommand.InputFiles, baseCommand.Daemons)
        {
            CallbackUrl = callbackUrl;
        }

        public string CallbackUrl { get; set; }
    }
}