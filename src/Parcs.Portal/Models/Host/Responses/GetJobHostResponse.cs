using Parcs.Portal.Models.Host.Responses.Nested;

namespace Parcs.Portal.Models.Host.Responses
{
    public class GetJobHostResponse
    {
        public long JobId { get; set; }

        public long ModuleId { get; set; }

        public string ModuleName { get; set; }

        public IEnumerable<JobStatusResponse> Statuses { get; set; }

        public IEnumerable<JobFailureResponse> Failures { get; set; }
    }
}