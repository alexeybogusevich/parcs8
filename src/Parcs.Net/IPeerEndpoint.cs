namespace Parcs.Net
{
    /// <summary>
    /// A listener opened by a running module so that other points can open channels to it
    /// directly, instead of through the point that created them. Pass <see cref="Address"/> to
    /// the peers (over any channel); each calls <see cref="IModuleInfo.ConnectAsync"/> with it.
    /// One endpoint accepts any number of channels, in the order the peers connect.
    /// </summary>
    public interface IPeerEndpoint : IAsyncDisposable
    {
        /// <summary>Opaque address the peers pass to <see cref="IModuleInfo.ConnectAsync"/>.</summary>
        string Address { get; }

        /// <summary>Waits for the next peer to connect and returns the channel to it.</summary>
        Task<IChannel> AcceptAsync(CancellationToken cancellationToken = default);
    }
}
