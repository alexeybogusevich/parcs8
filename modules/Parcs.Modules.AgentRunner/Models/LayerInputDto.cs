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

    /// <summary>Default receive timeout of the peer channels, and the timeout for connecting one.</summary>
    public int ChannelTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When true, WorkerInputs and the parents' per-index outputs are in worker_{index}.json
    /// instead of this file, so a worker reads only its own share.
    /// </summary>
    public bool PerWorkerFiles { get; set; }

    /// <summary>"all" (default): every worker gets every parent's whole result. "byIndex": worker i gets output i of each parent.</summary>
    public string Inherit { get; set; } = "all";

    /// <summary>Points the layer may use in all, spawned ones included; 0 or TotalWorkers turns spawning off.</summary>
    public int MaxPoints { get; set; }

    /// <summary>How deep spawned points may nest below the workers.</summary>
    public int MaxDepth { get; set; }
}
