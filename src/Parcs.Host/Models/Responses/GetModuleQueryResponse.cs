using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Models.Responses
{
    public class GetModuleQueryResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public IEnumerable<JobResponse> Jobs { get; set; }
    }
}