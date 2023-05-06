using MediatR;

namespace Parcs.Host.Models.Commands
{
    public class CreateJobFailureCommand : IRequest
    {
        public long JobId { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }
    }
}