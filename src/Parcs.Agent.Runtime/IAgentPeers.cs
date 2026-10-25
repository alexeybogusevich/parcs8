namespace Parcs.Agent.Runtime;

/// <summary>
/// Direct channels to the other workers of the layer. Nothing is declared up front: the code
/// opens a channel to whichever worker it needs, when it needs it, so the layer's topology is
/// whatever graph of channels the workers build as they run. Either end may open a channel
/// first; both ends then get the same channel.
/// </summary>
public interface IAgentPeers
{
    /// <summary>
    /// The channel to <paramref name="worker"/>, opening it on first use. Returns at once: sends
    /// are buffered until the channel connects, and a receive waits for it.
    /// </summary>
    IAgentChannel this[int worker] { get; }

    /// <summary>Workers this worker has a connected channel with so far, in index order.</summary>
    IReadOnlyList<int> Connected { get; }

    /// <summary>
    /// Waits, up to the layer's channel timeout, for a worker this one has not asked for to open
    /// a channel to it, and returns that channel. For workers that learn their partners from
    /// whoever contacts them.
    /// </summary>
    Task<IAgentChannel> AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>As <see cref="AcceptAsync(CancellationToken)"/>, waiting up to <paramref name="timeout"/>.</summary>
    Task<IAgentChannel> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selective wait: the next message from whichever of <paramref name="workers"/> sends first,
    /// and who sent it, waiting up to the layer's channel timeout. Opens the channels it needs.
    /// When several have a message waiting, the first of them in <paramref name="workers"/> wins.
    /// </summary>
    Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, CancellationToken cancellationToken = default);

    /// <summary>As <see cref="ReceiveAnyAsync(IEnumerable{int}, CancellationToken)"/>, waiting up to <paramref name="timeout"/>.</summary>
    Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Peers of a worker that cannot open channels; every use throws with the reason.</summary>
public sealed class UnavailableAgentPeers(string reason) : IAgentPeers
{
    public IAgentChannel this[int worker] => throw new IOException(reason);

    public IReadOnlyList<int> Connected => [];

    public Task<IAgentChannel> AcceptAsync(CancellationToken cancellationToken = default) => throw new IOException(reason);

    public Task<IAgentChannel> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => throw new IOException(reason);

    public Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, CancellationToken cancellationToken = default) =>
        throw new IOException(reason);

    public Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        throw new IOException(reason);
}
