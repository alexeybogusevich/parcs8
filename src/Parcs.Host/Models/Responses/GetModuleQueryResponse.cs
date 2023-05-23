using Parcs.Host.Models.Responses.Nested;

namespace Parcs.Host.Models.Responses
{
    public class GetModuleQueryResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public IEnumerable<string> Files { get; set; }

        public IEnumerable<AssemblyMetadataResponse> Assemblies { get; set; }

        public IEnumerable<GetJobQueryResponse> Jobs { get; set; }
    }
}