using Parcs.Net;

namespace Parcs.Core.Models.Interfaces
{
    public interface IManagedChannel : IChannel
    {
        void SetCancellation(CancellationToken cancellationToken);
    }
}