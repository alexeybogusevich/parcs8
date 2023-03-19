using Parcs.Core;

namespace Parcs.HostAPI.Models.Responses
{
    public class RunJobSynchronouslyCommandResponse
    {
        public RunJobSynchronouslyCommandResponse(Job job)
        {
            ElapsedSeconds = job.ExecutionTime?.Seconds;
            JobStatus = job.Status;
            ErrorMessage = job.ErrorMessage;
            Result = job.Result;
        }

        public double? ElapsedSeconds { get; set; }

        public JobStatus JobStatus { get; set; }

        public string ErrorMessage { get; set; }

        public double? Result { get; set; }
    }
}