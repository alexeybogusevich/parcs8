using Parcs.Net;

namespace Parcs.Core.Models.Interfaces
{
    public interface IManagedChannel : IChannel
    {
        bool IsConnected { get; }

        void SetCancellation(CancellationToken cancellationToken);
    }
}