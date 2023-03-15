using Parcs.Core;

namespace Parcs.HostAPI.Models.Responses
{
    public class GetJobQueryResponse
    {
        public JobStatus JobStatus { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public DateTime? StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public double? Result { get; set; }

        public string ErrorMessage { get; set; }
    }
}