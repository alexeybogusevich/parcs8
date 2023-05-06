using MediatR;
using Parcs.Host.Models.Responses;

namespace Parcs.Host.Models.Queries
{
    public class GetAllModulesQuery : IRequest<IEnumerable<GetModuleQueryResponse>>
    {
        public GetAllModulesQuery()
        {
        }
    }
}