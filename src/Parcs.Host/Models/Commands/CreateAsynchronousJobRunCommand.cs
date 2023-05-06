using MediatR;
using Parcs.Host.Models.Commands.Base;

namespace Parcs.Host.Models.Commands
{
    public class CreateAsynchronousJobRunCommand : CreateJobRunCommand, IRequest
    {
        public string CallbackUri { get; set; }
    }
}