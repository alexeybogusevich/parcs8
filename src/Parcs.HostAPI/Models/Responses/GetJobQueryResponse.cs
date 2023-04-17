using Parcs.Core.Models;

namespace Parcs.HostAPI.Models.Responses
{
    public class GetJobQueryResponse
    {
        public JobStatus JobStatus { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public DateTime? StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public string ErrorMessage { get; set; }
    }
}