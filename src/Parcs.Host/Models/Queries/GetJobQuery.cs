using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Queries
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