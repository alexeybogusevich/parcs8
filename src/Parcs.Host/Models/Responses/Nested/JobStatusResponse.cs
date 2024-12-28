using Parcs.Core.Models;

namespace Parcs.Host.Models.Responses.Nested
{
    public class JobStatusResponse(JobStatus status, DateTime createDateUtc)
    {
        public JobStatus Status { get; set; } = status;

        public DateTime CreateDateUtc { get; set; } = createDateUtc;
    }
}