namespace Parcs.Modules.AgentRunner.Models;

public sealed class WorkerResult
{
    public int WorkerIndex { get; set; }
    public bool Success { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public double ElapsedSeconds { get; set; }
}
