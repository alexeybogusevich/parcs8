using Parcs.Core.Models;

namespace Parcs.Host.Models.Responses
{
    public class RunJobSynchronouslyCommandResponse
    {
        public RunJobSynchronouslyCommandResponse(JobStatus? jobStatus)
        {
            JobStatus = jobStatus;
        }

        public JobStatus? JobStatus { get; set; }
    }
}