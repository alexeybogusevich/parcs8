using MediatR;
using Parcs.HostAPI.Models.Commands.Base;

namespace Parcs.HostAPI.Models.Commands
{
    public class RunJobAsynchronouslyCommand : RunJobCommand, IRequest
    {
        public RunJobAsynchronouslyCommand(RunJobCommand baseCommand, string callbackUrl)
            : base(baseCommand.JobId, baseCommand.ArgumentsJsonDictionary, baseCommand.NumberOfDaemons)
        {
            CallbackUrl = callbackUrl;
        }

        public string CallbackUrl { get; set; }
    }
}