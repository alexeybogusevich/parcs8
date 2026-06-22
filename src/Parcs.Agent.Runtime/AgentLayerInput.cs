namespace Parcs.Agent.Runtime;

/// <summary>
/// Input passed to each worker when executing a layer of parallel computation.
/// </summary>
public sealed class AgentLayerInput
{
    /// <summary>Zero-based index of this worker within the parallel pool.</summary>
    public int WorkerIndex { get; init; }

    /// <summary>Total number of workers in the pool for this layer.</summary>
    public int TotalWorkers { get; init; }

    /// <summary>Stable session identifier (shared across all layers in a session).</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Identifier for this specific layer execution.</summary>
    public string LayerId { get; init; } = string.Empty;

    /// <summary>
    /// JSON-encoded output from the previous layer, if any.
    /// Agents can deserialize this to access upstream computation results.
    /// </summary>
    public string? PreviousLayerResultJson { get; init; }

    /// <summary>Arbitrary string payload provided by the agent when submitting the layer.</summary>
    public string? CustomData { get; init; }

    /// <summary>Named parameters provided by the agent at submission time.</summary>
    public Dictionary<string, string> Parameters { get; init; } = new();

    /// <summary>
    /// Absolute path to the dataset file on the shared NFS volume
    /// (/var/lib/storage/Datasets/…/dataset.bin), or null if no dataset was provided.
    /// The MCP server downloads the dataset once and writes it to this path;
    /// all daemon workers read from the same shared filesystem — no file transfer needed.
    /// Usage: var data = File.ReadAllBytes(input.DatasetPath!);
    /// </summary>
    public string? DatasetPath { get; init; }
}
