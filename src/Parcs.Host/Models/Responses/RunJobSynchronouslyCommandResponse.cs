using Parcs.Core.Models;

namespace Parcs.Host.Models.Responses
{
    public class RunJobSynchronouslyCommandResponse(JobStatus? jobStatus)
    {
        public JobStatus? JobStatus { get; set; } = jobStatus;
    }
}