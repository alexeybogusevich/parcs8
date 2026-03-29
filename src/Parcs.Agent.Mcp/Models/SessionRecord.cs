namespace Parcs.Agent.Mcp.Models;

public sealed class SessionRecord
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public long ParcsModuleId { get; set; }
    public string AssemblyName { get; init; } = "agent_computation.dll";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Source code snapshot — stored for diagnostics / re-compilation.</summary>
    public string SourceCode { get; init; } = string.Empty;
}
