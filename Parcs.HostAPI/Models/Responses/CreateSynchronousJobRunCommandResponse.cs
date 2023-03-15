using Parcs.Core;

namespace Parcs.HostAPI.Models.Responses
{
    public class CreateSynchronousJobRunCommandResponse
    {
        public double? ElapsedSeconds { get; set; }

        public JobStatus JobStatus { get; set; }

        public string ErrorMessage { get; set; }

        public double? Result { get; set; }
    }
}