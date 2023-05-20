using Parcs.Core.Models;

namespace Parcs.Portal.Models.Host.Responses.Nested
{
    public class JobStatusResponse
    {
        public JobStatusResponse(JobStatus status, DateTime createDateUtc)
        {
            Status = status;
            CreateDateUtc = createDateUtc;
        }

        public JobStatus Status { get; set; }

        public DateTime CreateDateUtc { get; set; }
    }
}