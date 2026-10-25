namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Worker → main, first message of a layer with a topology: the endpoints this worker opened,
/// one per neighbour with a higher index (keyed by that neighbour), or why it could not open them.
/// </summary>
public sealed class WorkerEndpoints
{
    public int WorkerIndex { get; set; }
    public Dictionary<int, string> Endpoints { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>Main → worker: the endpoint of each neighbour with a lower index, which this worker dials.</summary>
public sealed class WorkerDialPlan
{
    public Dictionary<int, string> Dial { get; set; } = new();
}

/// <summary>
/// Main → worker, after every worker has returned its result: peer channels may close now.
/// Closing earlier could reset a connection whose last messages the neighbour has not read yet.
/// </summary>
public sealed class WorkerRelease
{
}

/// <summary>Per-worker input file <c>worker_{index}.json</c>, written when the layer has per-worker data.</summary>
public sealed class WorkerInputFileDto
{
    public string? WorkerInput { get; set; }
    public List<string?> InheritedOutputs { get; set; } = new();
}
