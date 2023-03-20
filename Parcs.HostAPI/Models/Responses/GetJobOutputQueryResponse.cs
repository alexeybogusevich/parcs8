using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Models.Responses
{
    public class GetJobOutputQueryResponse
    {
        public IEnumerable<FileDescription> FileDescriptions { get; set; }
    }
}