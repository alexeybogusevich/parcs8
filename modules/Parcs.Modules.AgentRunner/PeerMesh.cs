using System.Threading.Channels;
using Parcs.Agent.Runtime;
using Parcs.Net;

namespace Parcs.Modules.AgentRunner;

/// <summary>
/// A worker's channels to the other workers of its layer, opened on demand: the topology is
/// whatever graph the user code builds by asking for channels. Every worker listens on one
/// endpoint and holds the directory of everyone's endpoints, which the main module relays.
///
/// Whichever end asks first dials. The dialer sends its index; the listener answers Accepted or
/// Rejected before any data flows. When both ends dial at once, the connection dialed by the
/// lower index wins: a worker rejects a dial from a higher index while it is dialing that worker
/// itself, and the higher index then takes the lower one's connection. A pair never ends up with
/// two channels, and no message is sent on a connection that gets dropped.
/// </summary>
internal sealed class PeerMesh : IAgentPeers, IAsyncDisposable
{
    private const int Accepted = 1;
    private const int Rejected = 0;

    private readonly int _index;
    private readonly IReadOnlyList<string?> _directory;
    private readonly IPeerEndpoint _endpoint;
    private readonly Func<string, Task<IChannel>> _connect;
    private readonly TimeSpan _timeout;
    private readonly Dictionary<int, Link> _links = new();
    private readonly Channel<int> _unclaimed = Channel.CreateUnbounded<int>();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _acceptLoop;
    private int _disposed;

    public PeerMesh(int index, IReadOnlyList<string?> directory, IPeerEndpoint endpoint, Func<string, Task<IChannel>> connect, TimeSpan timeout)
    {
        _index      = index;
        _directory  = directory;
        _endpoint   = endpoint;
        _connect    = connect;
        _timeout    = timeout;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public IAgentChannel this[int worker]
    {
        get
        {
            if (worker < 0 || worker >= _directory.Count)
                throw new ArgumentOutOfRangeException(nameof(worker), worker, $"The layer has workers 0..{_directory.Count - 1}.");
            if (worker == _index)
                throw new ArgumentException($"Worker {worker} cannot open a channel to itself.", nameof(worker));

            Link link;
            bool dial;
            lock (_links)
            {
                link = LinkTo(worker);
                link.Claimed = true;
                dial = link.State == LinkState.Idle;
                if (dial)
                    link.State = LinkState.Dialing;
            }

            if (dial)
                _ = DialAsync(worker, link);
            return link.Channel;
        }
    }

    public IReadOnlyList<int> Connected
    {
        get
        {
            lock (_links)
            {
                return [.. _links.Where(l => l.Value.Connection.Task.IsCompletedSuccessfully).Select(l => l.Key).Order()];
            }
        }
    }

    public Task<IAgentChannel> AcceptAsync(CancellationToken cancellationToken = default) =>
        AcceptAsync(_timeout, cancellationToken);

    public async Task<IAgentChannel> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var worker = await _unclaimed.Reader.ReadAsync(timeoutSource.Token);
                lock (_links)
                {
                    // Skip workers the code asked for by index in the meantime.
                    var link = _links[worker];
                    if (link.Claimed)
                        continue;
                    link.Claimed = true;
                    return link.Channel;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"channel_timeout: no other worker opened a channel to worker {_index} within {timeout.TotalSeconds:0.#}s.");
        }
    }

    /// <summary>Waits, best-effort, until every channel has written what was sent on it.</summary>
    public Task FlushAsync()
    {
        lock (_links)
        {
            return Task.WhenAll(_links.Values.Select(l => l.Channel.FlushAsync()));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _stop.Cancel();
        await _endpoint.DisposeAsync();
        await _acceptLoop.ContinueWith(_ => { }, TaskScheduler.Default);

        Link[] links;
        lock (_links)
        {
            links = [.. _links.Values];
        }
        foreach (var link in links)
        {
            link.Connection.TrySetCanceled();
            await link.Channel.DisposeAsync();
        }
    }

    private Link LinkTo(int worker)
    {
        if (!_links.TryGetValue(worker, out var link))
        {
            link = new Link(worker, _timeout);
            _links[worker] = link;
        }
        return link;
    }

    private async Task DialAsync(int worker, Link link)
    {
        IChannel? channel = null;
        try
        {
            var address = _directory[worker]
                ?? throw new IOException($"worker {worker} has no endpoint (it failed before opening one).");

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeoutSource.CancelAfter(_timeout);

            channel = await _connect(address).WaitAsync(timeoutSource.Token);
            await channel.WriteDataAsync(_index).AsTask().WaitAsync(timeoutSource.Token);
            var answer = await channel.ReadIntAsync().WaitAsync(timeoutSource.Token);

            if (answer == Accepted && link.Connection.TrySetResult(channel))
                return;

            // Rejected: the other worker is dialing this one, and its connection is the one kept.
            channel.Dispose();
        }
        catch (Exception ex)
        {
            channel?.Dispose();
            if (_stop.IsCancellationRequested)
                link.Connection.TrySetCanceled();
            else
                link.Connection.TrySetException(new IOException($"Could not open a channel to worker {worker}: {ex.Message}", ex));
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            IChannel channel;
            try
            {
                channel = await _endpoint.AcceptAsync(_stop.Token);
            }
            catch
            {
                return;
            }
            _ = HandshakeAsync(channel);
        }
    }

    private async Task HandshakeAsync(IChannel channel)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeoutSource.CancelAfter(_timeout);

            var worker = await channel.ReadIntAsync().WaitAsync(timeoutSource.Token);
            if (worker < 0 || worker >= _directory.Count || worker == _index)
            {
                channel.Dispose();
                return;
            }

            Link link;
            bool accept, announce;
            lock (_links)
            {
                link = LinkTo(worker);
                accept = link.State == LinkState.Idle || (link.State == LinkState.Dialing && worker < _index);
                if (accept)
                    link.State = LinkState.Connected;
                announce = accept && !link.Claimed;
            }

            await channel.WriteDataAsync(accept ? Accepted : Rejected).AsTask().WaitAsync(timeoutSource.Token);
            if (!accept || !link.Connection.TrySetResult(channel))
            {
                channel.Dispose();
                return;
            }

            if (announce)
                _unclaimed.Writer.TryWrite(worker);
        }
        catch
        {
            channel.Dispose();
        }
    }

    private enum LinkState
    {
        Idle,
        Dialing,
        Connected,
    }

    private sealed class Link
    {
        public Link(int peer, TimeSpan timeout)
        {
            Channel = new PeerChannel(peer, Connection.Task, timeout);
        }

        public TaskCompletionSource<IChannel> Connection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PeerChannel Channel { get; }

        public LinkState State { get; set; }

        /// <summary>The code asked for this channel by index, or got it from AcceptAsync.</summary>
        public bool Claimed { get; set; }
    }
}
