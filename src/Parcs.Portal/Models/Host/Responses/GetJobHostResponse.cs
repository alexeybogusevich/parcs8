using Parcs.Portal.Models.Host.Responses.Nested;

namespace Parcs.Portal.Models.Host.Responses
{
    public class GetJobHostResponse
    {
        public long Id { get; set; }

        public long ModuleId { get; set; }

        public string ModuleName { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public bool HasOutput { get; set; }

        public IEnumerable<JobStatusResponse> Statuses { get; set; }

        public IEnumerable<JobFailureResponse> Failures { get; set; }

        public IEnumerable<string> Options { get; set; }
    }
}