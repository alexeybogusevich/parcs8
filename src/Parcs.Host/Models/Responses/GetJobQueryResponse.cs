using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Models.Responses
{
    public class GetJobQueryResponse
    {
        public long Id { get; set; }

        public long ModuleId { get; set; }

        public string ModuleName { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public IEnumerable<JobStatusResponse> Statuses { get; set; }

        public IEnumerable<JobFailureResponse> Failures { get; set; }
    }
}