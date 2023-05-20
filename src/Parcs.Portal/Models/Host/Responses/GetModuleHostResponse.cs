using Parcs.Portal.Models.Host.Responses.Nested;

namespace Parcs.Portal.Models.Host.Responses
{
    public class GetModuleHostResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public IEnumerable<JobResponse> Jobs { get; set; }
    }
}