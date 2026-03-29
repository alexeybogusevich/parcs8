namespace Parcs.Modules.AgentRunner.Models;

public sealed class LayerOutputDto
{
    public string SessionId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public List<WorkerResult> Results { get; set; } = new();
    public double TotalElapsedSeconds { get; set; }
    public bool AnyFailures => Results.Any(r => !r.Success);
}
