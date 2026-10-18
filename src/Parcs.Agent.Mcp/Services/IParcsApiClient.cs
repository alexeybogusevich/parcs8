namespace Parcs.Agent.Mcp.Services;

/// <summary>The PARCS Host API operations the MCP server uses.</summary>
public interface IParcsApiClient
{
    Task<long> CreateJobAsync(
        long moduleId,
        string assemblyName,
        string className,
        IEnumerable<(string Filename, byte[] Bytes)> inputFiles,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct = default);

    /// <summary>Starts the job and returns once it is Completed; throws when it Fails or is Cancelled.</summary>
    Task RunJobAndWaitAsync(long jobId, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken ct = default);

    Task<byte[]?> GetJobOutputFileAsync(long jobId, string filename, CancellationToken ct = default);

    Task CancelJobAsync(long jobId, CancellationToken ct = default);
}

/// <summary>The registered AgentRunner module that hosts compiled user code.</summary>
public interface IAgentRunnerModule
{
    bool IsReady { get; }

    long ModuleId { get; }

    string AssemblyName { get; }

    string ClassName { get; }
}
