namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Serialized to <c>layer_input.json</c> and passed as a job input file.
/// Contains all layer-level parameters; the worker-specific index is injected by the main module.
/// </summary>
public sealed class LayerInputDto
{
    public string SessionId { get; set; } = string.Empty;

    public string LayerId { get; set; } = string.Empty;

    public int TotalWorkers { get; set; } = 1;

    /// <summary>
    /// Full name of the IAgentComputation class to run; null when the assembly has exactly one.
    /// </summary>
    public string? EntryPoint { get; set; }

    /// <summary>When true, the first failed worker fails the whole layer.</summary>
    public bool FailFast { get; set; }

    /// <summary>Stored results of the parent layers, in the order the agent listed them.</summary>
    public List<string> PreviousLayerResults { get; set; } = new();

    public string? CustomData { get; set; }

    /// <summary>One payload per worker (index = WorkerIndex), or null for broadcast-only layers.</summary>
    public List<string?>? WorkerInputs { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>Topology of peer channels (AgentTopologySpec text form); null or "none" for none.</summary>
    public string? Topology { get; set; }

    /// <summary>Edges of a custom topology.</summary>
    public List<int[]>? TopologyEdges { get; set; }

    /// <summary>Default receive timeout of the peer channels, and the timeout for wiring them.</summary>
    public int ChannelTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When true, WorkerInputs and the parents' per-index outputs are in worker_{index}.json
    /// instead of this file, so a worker reads only its own share.
    /// </summary>
    public bool PerWorkerFiles { get; set; }

    /// <summary>"all" (default): every worker gets every parent's whole result. "byIndex": worker i gets output i of each parent.</summary>
    public string Inherit { get; set; } = "all";
}
