namespace Parcs.Net
{
    /// <summary>
    /// A connection slot opened by a running module so that another point can open a channel
    /// to it directly, instead of through the point that created them. Pass <see cref="Address"/>
    /// to the peer (over any channel), which calls <see cref="IModuleInfo.ConnectAsync"/> with
    /// it. Each endpoint accepts exactly one channel.
    /// </summary>
    public interface IPeerEndpoint : IAsyncDisposable
    {
        /// <summary>Opaque address the peer passes to <see cref="IModuleInfo.ConnectAsync"/>.</summary>
        string Address { get; }

        /// <summary>Waits until the peer has connected and returns the channel to it.</summary>
        Task<IChannel> AcceptAsync(CancellationToken cancellationToken = default);
    }
}
