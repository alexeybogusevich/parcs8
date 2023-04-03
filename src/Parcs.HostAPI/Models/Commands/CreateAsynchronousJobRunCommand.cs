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
            : base(baseCommand.ModuleId, baseCommand.MainModuleAssemblyName, baseCommand.MainModuleClassName, baseCommand.InputFiles, baseCommand.JsonArgumentsDictionary, baseCommand.NumberOfDaemons)
        {
            CallbackUri = callbackUrl;
        }

        public string CallbackUri { get; set; }
    }
}