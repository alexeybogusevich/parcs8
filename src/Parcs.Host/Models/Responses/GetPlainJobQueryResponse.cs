using Parcs.Core.Models;

namespace Parcs.Host.Models.Responses
{
    public class GetPlainJobQueryResponse
    {
        public long Id { get; set; }

        public long ModuleId { get; set; }

        public string ModuleName { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public JobStatus Status { get; set; }
    }
}