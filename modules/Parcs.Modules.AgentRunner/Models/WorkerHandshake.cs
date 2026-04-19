namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Minimal per-worker message sent from the main module over the TCP channel.
/// Everything else (assembly, layer input) is read directly from shared NFS storage.
/// </summary>
public sealed class WorkerHandshake
{
    public int WorkerIndex { get; set; }
}
