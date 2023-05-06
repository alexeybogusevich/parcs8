using Parcs.Core.Models;

namespace Parcs.HostAPI.Models.Responses
{
    public class RunJobSynchronouslyCommandResponse
    {
        public RunJobSynchronouslyCommandResponse(long jobId, JobStatus? jobStatus)
        {
            JobId = jobId;
            JobStatus = jobStatus;
        }

        public long JobId { get; set; }

        public JobStatus? JobStatus { get; set; }
    }
}