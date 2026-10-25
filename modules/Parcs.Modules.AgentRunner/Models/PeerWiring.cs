namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Worker → main, first message after the handshake: the endpoint other workers dial to open a
/// channel to this one, or why it could not be opened.
/// </summary>
public sealed class WorkerEndpoint
{
    public int WorkerIndex { get; set; }
    public string? Address { get; set; }
    public string? Error { get; set; }
}

/// <summary>Main → worker: every worker's endpoint by index (null for a worker without one).</summary>
public sealed class WorkerDirectory
{
    public List<string?> Addresses { get; set; } = new();
}

/// <summary>
/// Main → worker, after every worker has returned its result: peer channels may close now.
/// Closing earlier could reset a connection whose last messages the other worker has not read yet.
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
