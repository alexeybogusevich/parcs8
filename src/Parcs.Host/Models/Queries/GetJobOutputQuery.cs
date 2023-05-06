using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Queries
{
    public class GetJobOutputQuery : IRequest<GetJobOutputQueryResponse>
    {
        public GetJobOutputQuery()
        {
        }

        public GetJobOutputQuery(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}