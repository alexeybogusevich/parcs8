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
    /// For each parent layer, in <c>previousLayerIds</c> order, the output of the parent's worker
    /// with this worker's index (null when it failed or the parent had fewer workers). With
    /// <c>inherit: "byIndex"</c> this is all a worker receives of its parents.
    /// </summary>
    public IReadOnlyList<string?> InheritedOutputs { get; init; } = [];

    /// <summary>
    /// Direct channels to the other workers, opened on demand; see <see cref="Peer"/>.
    /// </summary>
    public IAgentPeers Peers { get; init; } = new UnavailableAgentPeers("Peer channels exist only inside a running PARCS layer.");

    /// <summary>
    /// The channel to worker <paramref name="worker"/>, opened the first time either side asks:
    /// the workers build the layer's topology as they run. Same as <c>Peers[worker]</c>.
    /// </summary>
    public IAgentChannel Peer(int worker) => Peers[worker];

    /// <summary>
    /// How deep this worker is: 0 for the layer's workers, 1 for points they spawned, and so on.
    /// A spawned worker's <see cref="WorkerIndex"/> and <see cref="TotalWorkers"/> count its siblings,
    /// and <see cref="Peer"/> reaches them.
    /// </summary>
    public int Depth { get; init; }

    /// <summary>The deepest a spawned point may be (the layer's maxDepth); 0 when spawning is off.</summary>
    public int MaxDepth { get; init; }

    /// <summary>For a spawned worker, the channel to the worker that spawned it; null at depth 0.</summary>
    public IAgentChannel? Spawner { get; init; }

    /// <summary>Spawning support, set by the runner; null outside a running PARCS layer.</summary>
    public IAgentSpawner? Spawning { get; init; }

    /// <summary>How many more points this worker may spawn below itself (its children's subtrees included).</summary>
    public int SpawnBudget => Spawning?.Remaining ?? 0;

    /// <summary>
    /// Recursion: starts <paramref name="count"/> new points that run this session's code one level
    /// deeper, <paramref name="entryPoint"/> (default: this worker's class) with
    /// <c>inputs[i]</c> as child i's <see cref="WorkerInput"/>. The children get an even split of
    /// the rest of this worker's budget, or <paramref name="budgetPerChild"/> each. Fails at once
    /// when the layer's maxDepth or points budget would be exceeded.
    /// </summary>
    public Task<IAgentSpawn> SpawnAsync(
        int count, string? entryPoint = null, IReadOnlyList<string?>? inputs = null, int? budgetPerChild = null,
        CancellationToken cancellationToken = default) =>
        Spawning?.SpawnAsync(count, entryPoint, inputs, budgetPerChild, cancellationToken)
        ?? throw new InvalidOperationException("Spawning is available only inside a running PARCS layer.");

    /// <summary>
    /// Returns the per-worker outputs of a parent layer in worker order, with null for workers
    /// that failed. <paramref name="parentIndex"/> selects the parent when the layer has several.
    /// Empty with <c>inherit: "byIndex"</c>; use <see cref="InheritedOutputs"/> then.
    /// </summary>
    public IReadOnlyList<string?> GetPreviousOutputs(int parentIndex = 0) =>
        parentIndex < PreviousLayerResults.Count
            ? LayerResultDocument.ReadOutputs(PreviousLayerResults[parentIndex])
            : [];
}
