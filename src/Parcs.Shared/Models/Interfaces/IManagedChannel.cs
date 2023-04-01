using Parcs.Net;

namespace Parcs.Shared.Models.Interfaces
{
    public interface IManagedChannel : IChannel
    {
        void SetCancellation(CancellationToken cancellationToken);
    }
}