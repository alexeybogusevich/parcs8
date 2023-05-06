using Parcs.Host.Models.Domain;

namespace Parcs.Host.Models.Responses
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