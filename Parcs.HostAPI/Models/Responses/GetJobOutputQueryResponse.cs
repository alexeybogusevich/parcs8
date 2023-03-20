using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Models.Responses
{
    public class GetJobOutputQueryResponse
    {
        public GetJobOutputQueryResponse()
        {
        }

        public GetJobOutputQueryResponse(FileDescription archivedOutput)
        {
            ArchivedOutput = archivedOutput;
        }

        public FileDescription ArchivedOutput { get; set; }
    }
}