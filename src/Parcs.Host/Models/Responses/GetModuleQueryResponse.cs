using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Responses
{
    public class GetModuleQueryResponse
    {
        public string Name { get; set; }

        public IEnumerable<GetJobQueryResponse> Jobs { get; set; }
    }
}