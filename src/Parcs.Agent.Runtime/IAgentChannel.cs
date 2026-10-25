using System.Text.Json;

namespace Parcs.Agent.Runtime;

/// <summary>
/// A channel to one other worker of the layer. Sends are buffered and never block, so two
/// workers that both send before they receive do not deadlock. A receive waits at most the
/// layer's channel timeout and then throws <see cref="TimeoutException"/>; it throws
/// <see cref="IOException"/> once the other worker has failed or closed the channel, or could
/// not be reached. Messages arrive in the order they were sent.
/// </summary>
public interface IAgentChannel
{
    /// <summary>Worker index at the other end.</summary>
    int Peer { get; }

    Task SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Waits for the next message, up to the layer's channel timeout.</summary>
    Task<string> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Waits for the next message, up to <paramref name="timeout"/>.</summary>
    Task<string> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Whether a message has arrived and is waiting to be received. Never waits.</summary>
    bool HasMessage { get; }

    /// <summary>Takes the next message if one has arrived; never waits.</summary>
    bool TryReceive(out string message);
}

public static class AgentChannelExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Sends <paramref name="value"/> as JSON.</summary>
    public static Task SendJsonAsync<T>(this IAgentChannel channel, T value, CancellationToken cancellationToken = default) =>
        channel.SendAsync(JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    /// <summary>Receives the next message and parses it as JSON.</summary>
    public static async Task<T> ReceiveJsonAsync<T>(this IAgentChannel channel, CancellationToken cancellationToken = default) =>
        JsonSerializer.Deserialize<T>(await channel.ReceiveAsync(cancellationToken), JsonOptions)!;

    /// <summary>Takes the next message as JSON if one has arrived; never waits.</summary>
    public static bool TryReceiveJson<T>(this IAgentChannel channel, out T value)
    {
        value = default!;
        if (!channel.TryReceive(out var message))
            return false;
        value = JsonSerializer.Deserialize<T>(message, JsonOptions)!;
        return true;
    }

    /// <summary>The next message from any of <paramref name="workers"/>, parsed as JSON, and who sent it.</summary>
    public static async Task<(int Peer, T Value)> ReceiveAnyJsonAsync<T>(
        this IAgentPeers peers, IEnumerable<int> workers, CancellationToken cancellationToken = default)
    {
        var (peer, message) = await peers.ReceiveAnyAsync(workers, cancellationToken);
        return (peer, JsonSerializer.Deserialize<T>(message, JsonOptions)!);
    }

    /// <summary>Sends <paramref name="value"/> and returns the neighbour's next message: one step of an exchange.</summary>
    public static async Task<T> ExchangeJsonAsync<T>(this IAgentChannel channel, T value, CancellationToken cancellationToken = default)
    {
        await channel.SendJsonAsync(value, cancellationToken);
        return await channel.ReceiveJsonAsync<T>(cancellationToken);
    }
}
