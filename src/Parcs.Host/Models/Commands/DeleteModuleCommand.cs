using MediatR;

namespace Parcs.Host.Models.Commands
{
    public class DeleteModuleCommand : IRequest
    {
        public long Id { get; set; }
    }
}