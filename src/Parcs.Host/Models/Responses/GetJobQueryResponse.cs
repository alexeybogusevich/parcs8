using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Models.Responses
{
    public class GetJobQueryResponse
    {
        public long ModuleId { get; set; }

        public string ModuleName { get; set; }

        public IEnumerable<JobStatusResponse> Statuses { get; set; }

        public IEnumerable<JobFailureResponse> Failures { get; set; }
    }
}