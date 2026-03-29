namespace Parcs.Agent.Mcp.Models;

public enum LayerStatus { Pending, Running, Completed, Failed }

public sealed class LayerRecord
{
    public string LayerId { get; init; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; init; } = string.Empty;
    public LayerStatus Status { get; set; } = LayerStatus.Pending;
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
