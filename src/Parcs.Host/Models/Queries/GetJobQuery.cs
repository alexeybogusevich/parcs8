using MediatR;
using Parcs.HostAPI.Models.Responses;

namespace Parcs.HostAPI.Models.Queries
{
    public class GetJobQuery : IRequest<GetJobQueryResponse>
    {
        public GetJobQuery()
        {
        }

        public GetJobQuery(long jobId)
        {
            JobId = jobId;
        }

        public long JobId { get; set; }
    }
}