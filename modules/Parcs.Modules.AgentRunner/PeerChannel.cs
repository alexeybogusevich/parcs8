using System.Threading.Channels;
using Parcs.Agent.Runtime;
using Parcs.Net;

namespace Parcs.Modules.AgentRunner;

/// <summary>
/// <see cref="IAgentChannel"/> over a PARCS peer channel that may still be connecting. A background
/// writer drains an unbounded outbox and a background reader fills an inbox, so
/// <see cref="SendAsync"/> never blocks on the socket: workers that both send first cannot
/// deadlock, however large the messages, and sends made before the channel connects go out once
/// it does.
/// </summary>
internal sealed class PeerChannel : IAgentChannel, IAsyncDisposable
{
    private readonly Task<IChannel> _connection;
    private readonly TimeSpan _defaultTimeout;
    private readonly Channel<string> _outbox = Channel.CreateUnbounded<string>(new() { SingleReader = true });
    private readonly Channel<string> _inbox = Channel.CreateUnbounded<string>(new() { SingleWriter = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _writer;
    private readonly Task _reader;
    private Exception? _writeFailure;

    public PeerChannel(int peer, Task<IChannel> connection, TimeSpan defaultTimeout)
    {
        Peer            = peer;
        _connection     = connection;
        _defaultTimeout = defaultTimeout;
        _writer         = Task.Run(WriteLoopAsync);
        _reader         = Task.Run(ReadLoopAsync);
    }

    public int Peer { get; }

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_writeFailure is { } failure)
            throw new IOException($"Sending to worker {Peer} failed: {failure.Message}", failure);
        if (!_outbox.Writer.TryWrite(message))
            throw new InvalidOperationException($"The channel to worker {Peer} is closed.");
        return Task.CompletedTask;
    }

    public Task<string> ReceiveAsync(CancellationToken cancellationToken = default) =>
        ReceiveAsync(_defaultTimeout, cancellationToken);

    public async Task<string> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            return await _inbox.Reader.ReadAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"channel_timeout: no message from worker {Peer} within {timeout.TotalSeconds:0.#}s" +
                (_connection.IsCompleted ? ". " : " (the channel never connected). ") +
                "Check that every receive has a matching send on the other worker.");
        }
        catch (ChannelClosedException ex)
        {
            throw Closed(ex.InnerException ?? ex);
        }
    }

    public bool HasMessage => _inbox.Reader.TryPeek(out _);

    public bool TryReceive(out string message) => _inbox.Reader.TryRead(out message!);

    /// <summary>Waits until a message can be received; throws <see cref="IOException"/> once none ever will.</summary>
    internal async Task WaitForMessageAsync(CancellationToken cancellationToken)
    {
        bool open;
        try
        {
            open = await _inbox.Reader.WaitToReadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Closed(ex is ChannelClosedException { InnerException: { } inner } ? inner : ex);
        }
        if (!open)
            throw Closed(null);
    }

    private IOException Closed(Exception? cause) => _connection.IsFaulted
        ? new IOException(cause?.Message ?? $"No channel to worker {Peer}.", cause)
        : new IOException($"Worker {Peer} closed the channel (it finished or failed) before sending the expected message.", cause);

    /// <summary>
    /// Waits, at most the default timeout, until everything sent so far has been written to the
    /// socket. Best-effort: a worker that never connected gets nothing, and its own receive reports it.
    /// </summary>
    public async Task FlushAsync()
    {
        _outbox.Writer.TryComplete();
        await _writer.WaitAsync(_defaultTimeout).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public async ValueTask DisposeAsync()
    {
        _outbox.Writer.TryComplete();
        _stop.Cancel();
        if (_connection.IsCompletedSuccessfully)
            _connection.Result.Dispose();
        await Task.WhenAll(_writer, _reader).ContinueWith(_ => { }, TaskScheduler.Default);
        _stop.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            var channel = await _connection.WaitAsync(_stop.Token);
            await foreach (var message in _outbox.Reader.ReadAllAsync(_stop.Token))
            {
                await channel.WriteDataAsync(message);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _writeFailure = ex;
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            var channel = await _connection.WaitAsync(_stop.Token);
            while (!_stop.IsCancellationRequested)
            {
                var message = await channel.ReadStringAsync();
                _inbox.Writer.TryWrite(message);
            }
            _inbox.Writer.TryComplete();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            _inbox.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            _inbox.Writer.TryComplete(ex);
        }
    }
}
