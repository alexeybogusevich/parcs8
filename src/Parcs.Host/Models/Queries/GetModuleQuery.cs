using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Queries
{
    public class GetModuleQuery : IRequest<GetModuleQueryResponse>
    {
        public long Id { get; set; }
    }
}