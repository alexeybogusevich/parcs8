using System.Text.Json;

namespace Parcs.Agent.Runtime;

/// <summary>
/// A channel to one neighbouring worker of the layer's topology. Sends are buffered and never
/// block, so two neighbours that both send before they receive do not deadlock. A receive waits
/// at most the layer's channel timeout and then throws <see cref="TimeoutException"/>; it throws
/// <see cref="IOException"/> once the neighbour has failed or closed the channel.
/// Messages arrive in the order they were sent.
/// </summary>
public interface IAgentChannel
{
    /// <summary>Worker index of the neighbour at the other end.</summary>
    int Peer { get; }

    Task SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Waits for the next message, up to the layer's channel timeout.</summary>
    Task<string> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Waits for the next message, up to <paramref name="timeout"/>.</summary>
    Task<string> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
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

    /// <summary>Sends <paramref name="value"/> and returns the neighbour's next message: one step of an exchange.</summary>
    public static async Task<T> ExchangeJsonAsync<T>(this IAgentChannel channel, T value, CancellationToken cancellationToken = default)
    {
        await channel.SendJsonAsync(value, cancellationToken);
        return await channel.ReceiveJsonAsync<T>(cancellationToken);
    }
}
