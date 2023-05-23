using Parcs.Portal.Models.Host.Responses.Nested;

namespace Parcs.Portal.Models.Host.Responses
{
    public class GetModuleHostResponse
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public IEnumerable<string> Files { get; set; }

        public IEnumerable<AssemblyMetadataResponse> Assemblies { get; set; }

        public IEnumerable<GetJobHostResponse> Jobs { get; set; }
    }
}