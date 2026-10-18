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
    /// JSON-encoded output of the first (or only) parent layer, if any — the same document as
    /// <c>PreviousLayerResults[0]</c>. Use <see cref="GetPreviousOutputs"/> to read the parent's
    /// per-worker outputs without parsing this envelope yourself.
    /// </summary>
    public string? PreviousLayerResultJson { get; init; }

    /// <summary>
    /// JSON-encoded outputs of every parent layer, in the order the parents were listed in
    /// <c>previousLayerIds</c>. Empty for a first layer.
    /// </summary>
    public IReadOnlyList<string> PreviousLayerResults { get; init; } = [];

    /// <summary>Arbitrary string payload provided by the agent, identical for every worker.</summary>
    public string? CustomData { get; init; }

    /// <summary>
    /// The payload provided for this worker only (<c>workerInputs[WorkerIndex]</c>), or null
    /// when the layer was started without per-worker inputs.
    /// </summary>
    public string? WorkerInput { get; init; }

    /// <summary>Named parameters provided by the agent, identical for every worker.</summary>
    public Dictionary<string, string> Parameters { get; init; } = new();

    /// <summary>
    /// Returns the per-worker outputs of a parent layer in worker order, with null for workers
    /// that failed. <paramref name="parentIndex"/> selects the parent when the layer has several.
    /// </summary>
    public IReadOnlyList<string?> GetPreviousOutputs(int parentIndex = 0) =>
        parentIndex < PreviousLayerResults.Count
            ? LayerResultDocument.ReadOutputs(PreviousLayerResults[parentIndex])
            : [];
}
