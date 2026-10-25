using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    /// <summary>
    /// Direct channels between running points. An endpoint is a listener on this process's
    /// <see cref="CallbackTcpServer"/> (the Host and every daemon run one), so peers connect the
    /// same way a queue-provisioned daemon dials back to its parent.
    /// </summary>
    public interface IPeerChannelService
    {
        IPeerEndpoint OpenEndpoint(CancellationToken cancellationToken);

        Task<IChannel> ConnectAsync(string endpointAddress, CancellationToken cancellationToken);
    }
}
