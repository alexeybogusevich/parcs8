using Parcs.Core;

namespace Parcs.HostAPI.Models.Responses
{
    public class CreateJobCommandResponse
    {
        public double? ElapsedSeconds { get; set; }

        public JobStatus JobStatus { get; set; }

        public string ErrorMessage { get; set; }

        public double? Result { get; set; }
    }
}