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
}
