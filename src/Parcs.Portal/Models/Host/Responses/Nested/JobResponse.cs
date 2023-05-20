namespace Parcs.Portal.Models.Host.Responses.Nested
{
    public class JobResponse
    {
        public long JobId { get; set; }

        public IEnumerable<JobStatusResponse> Statuses { get; set; }

        public IEnumerable<JobFailureResponse> Failures { get; set; }
    }
}