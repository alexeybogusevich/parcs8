namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Minimal per-worker message sent by the main module, or by a worker that spawned this point,
/// over the TCP channel. Everything else (assembly, layer input) is read directly from shared
/// NFS storage.
/// </summary>
public sealed class WorkerHandshake
{
    /// <summary>Index among the layer's workers, or among the siblings of a spawned point.</summary>
    public int WorkerIndex { get; set; }

    /// <summary>0 for the layer's workers; one more for each level of spawning.</summary>
    public int Depth { get; set; }

    public int MaxDepth { get; set; }

    /// <summary>Points this worker may spawn below itself.</summary>
    public int SpawnBudget { get; set; }

    /// <summary>Set for a spawned point only: how many siblings it has, which class runs and its input.</summary>
    public int? Siblings { get; set; }

    public string? EntryPoint { get; set; }

    public string? WorkerInput { get; set; }

    public bool IsSpawned => Siblings is not null;
}
