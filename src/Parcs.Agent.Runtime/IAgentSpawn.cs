using System.Text.Json;

namespace Parcs.Agent.Runtime;

/// <summary>
/// Points a worker spawned with <see cref="AgentLayerInput.SpawnAsync"/>: they run this
/// session's code one level deeper, each with its own channel back to the spawning worker.
/// </summary>
public interface IAgentSpawn
{
    /// <summary>Number of spawned points.</summary>
    int Count { get; }

    /// <summary>The channel to spawned point <paramref name="index"/> (its <c>input.Spawner</c> at the other end).</summary>
    IAgentChannel Child(int index);

    /// <summary>
    /// Waits for every spawned point to finish and returns their outputs in index order. Throws
    /// <see cref="InvalidOperationException"/> naming the first one that failed.
    /// </summary>
    Task<IReadOnlyList<string?>> ResultsAsync(CancellationToken cancellationToken = default);
}

/// <summary>What a running worker uses to spawn points; the AgentRunner module provides it.</summary>
public interface IAgentSpawner
{
    /// <summary>How many more points this worker may spawn below itself, its children's subtrees included.</summary>
    int Remaining { get; }

    Task<IAgentSpawn> SpawnAsync(
        int count, string? entryPoint, IReadOnlyList<string?>? inputs, int? budgetPerChild, CancellationToken cancellationToken);
}

public static class AgentSpawnExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The spawned points' outputs parsed as JSON, in index order.</summary>
    public static async Task<IReadOnlyList<T>> ResultsJsonAsync<T>(this IAgentSpawn spawn, CancellationToken cancellationToken = default) =>
        [.. (await spawn.ResultsAsync(cancellationToken)).Select(r => JsonSerializer.Deserialize<T>(r ?? "null", JsonOptions)!)];
}
