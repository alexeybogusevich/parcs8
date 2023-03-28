using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Queries
{
    public class GetJobOutputQuery : IRequest<GetJobOutputQueryResponse>
    {
        public GetJobOutputQuery()
        {
        }

        public GetJobOutputQuery(Guid jobId)
        {
            JobId = jobId;
        }

        public Guid JobId { get; set; }
    }
}