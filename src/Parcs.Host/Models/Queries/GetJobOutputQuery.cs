using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Queries
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