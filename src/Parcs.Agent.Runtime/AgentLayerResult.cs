namespace Parcs.Agent.Runtime;

/// <summary>
/// Result returned by a worker after executing a layer of parallel computation.
/// </summary>
public sealed class AgentLayerResult
{
    public bool Success { get; init; } = true;

    /// <summary>Primary output of this worker — any JSON or plain string payload.</summary>
    public string? OutputData { get; init; }

    /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Optional key/value metadata (e.g. timing, counters, intermediate stats).</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    public static AgentLayerResult Ok(string? outputData = null, Dictionary<string, string>? metadata = null)
        => new() { Success = true, OutputData = outputData, Metadata = metadata ?? new() };

    public static AgentLayerResult Error(string message)
        => new() { Success = false, ErrorMessage = message };
}
