namespace Parcs.Agent.Mcp.Models;

public sealed class SessionRecord
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Source code snapshot — stored for diagnostics / re-compilation.</summary>
    public string SourceCode { get; init; } = string.Empty;

    /// <summary>Hash of the normalised source; the same source maps to the same session.</summary>
    public required string SourceHash { get; init; }

    /// <summary>Full names of the IAgentComputation classes in the compiled assembly.</summary>
    public required IReadOnlyList<string> EntryPoints { get; init; }

    /// <summary>Compiled DLL, re-sent to PARCS with every layer job.</summary>
    public required byte[] Assembly { get; init; }
}
