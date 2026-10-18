using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Parcs.Agent.Mcp.Models;

/// <summary>
/// The single result shape of every PARCS tool, so code can check one structure:
/// <c>ok</c> is false exactly when <c>error</c> is set. A layer that is still Queued or Running
/// is ok; Failed, Cancelled and CompletedWithFailures are not.
/// </summary>
public sealed class ToolEnvelope
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    /// <summary>Layer: Queued | Running | Completed | CompletedWithFailures | Failed | Cancelled. Session: Created | Reused.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("layerId")]
    public string? LayerId { get; init; }

    [JsonPropertyName("layerKey")]
    public string? LayerKey { get; init; }

    /// <summary>Execution details of a layer; absent for other tools.</summary>
    [JsonPropertyName("layer")]
    public LayerSummary? Layer { get; init; }

    /// <summary>
    /// The tool's payload. For a layer with parallelism 1: that worker's output, parsed when it is
    /// JSON. For other tools: the object their description documents.
    /// </summary>
    [JsonPropertyName("output")]
    public JsonNode? Output { get; init; }

    /// <summary>view "outputs" or "full": every worker's output in worker order, null for failed workers.</summary>
    [JsonPropertyName("outputs")]
    public JsonArray? Outputs { get; init; }

    /// <summary>view "full": per-worker detail.</summary>
    [JsonPropertyName("workers")]
    public IReadOnlyList<WorkerDetail>? Workers { get; init; }

    [JsonPropertyName("error")]
    public ToolError? Error { get; init; }

    public static ToolEnvelope Fail(ToolError error) => new() { Ok = false, Error = error, LayerId = error.LayerId };
}

public sealed class LayerSummary
{
    [JsonPropertyName("entryPoint")]
    public string? EntryPoint { get; init; }

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; init; }

    [JsonPropertyName("previousLayerIds")]
    public IReadOnlyList<string> PreviousLayerIds { get; init; } = [];

    [JsonPropertyName("successCount")]
    public int? SuccessCount { get; init; }

    [JsonPropertyName("failureCount")]
    public int? FailureCount { get; init; }

    /// <summary>Seconds spent waiting for free workers before the job started.</summary>
    [JsonPropertyName("queuedSeconds")]
    public double QueuedSeconds { get; init; }

    /// <summary>Seconds from job start to completion (or to now while running).</summary>
    [JsonPropertyName("elapsedSeconds")]
    public double? ElapsedSeconds { get; init; }

    [JsonPropertyName("submittedAt")]
    public DateTimeOffset SubmittedAt { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    [JsonPropertyName("jobId")]
    public long? JobId { get; init; }
}

public sealed class WorkerDetail
{
    [JsonPropertyName("workerIndex")]
    public int WorkerIndex { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("output")]
    public JsonNode? Output { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("elapsedSeconds")]
    public double ElapsedSeconds { get; init; }
}

public sealed record ToolError
{
    /// <summary>One of <see cref="ErrorCodes"/>.</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>True when repeating the same call unchanged may succeed.</summary>
    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }

    [JsonPropertyName("layerId")]
    public string? LayerId { get; init; }

    [JsonPropertyName("failedWorkers")]
    public IReadOnlyList<int>? FailedWorkers { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<CompileDiagnostic>? Diagnostics { get; init; }
}

public sealed record CompileDiagnostic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column,
    [property: JsonPropertyName("message")] string Message);

/// <summary>The fixed list of error codes a tool can return.</summary>
public static class ErrorCodes
{
    /// <summary>An argument is missing, malformed or inconsistent with the session.</summary>
    public const string InvalidArgument = "invalid_argument";
    public const string SessionNotFound = "session_not_found";
    public const string LayerNotFound = "layer_not_found";
    /// <summary>layerKey was used before in this session with different arguments.</summary>
    public const string LayerKeyConflict = "layer_key_conflict";
    /// <summary>A previous layer is still Queued or Running (retryable).</summary>
    public const string PreviousLayerNotReady = "previous_layer_not_ready";
    /// <summary>A previous layer Failed or was Cancelled, so it has no results.</summary>
    public const string PreviousLayerFailed = "previous_layer_failed";
    public const string CompileError = "compile_error";
    /// <summary>parallelism is larger than maxParallelism; queuing cannot help.</summary>
    public const string CapacityExceeded = "capacity_exceeded";
    /// <summary>The AgentRunner module is not yet registered with the PARCS host (retryable).</summary>
    public const string NotReady = "not_ready";
    /// <summary>The computation threw or returned an error on one or more workers.</summary>
    public const string WorkerError = "worker_error";
    /// <summary>A worker ran out of memory: lower parallelism or per-worker data.</summary>
    public const string OutOfMemory = "out_of_memory";
    /// <summary>Daemons did not start or connect in time (retryable).</summary>
    public const string DaemonTimeout = "daemon_timeout";
    /// <summary>No daemon is available in a static pool (retryable once daemons are back).</summary>
    public const string NoDaemons = "no_daemons";
    /// <summary>The PARCS host could not be reached (retryable).</summary>
    public const string HostUnavailable = "host_unavailable";
    /// <summary>The PARCS job failed for a reason not covered above.</summary>
    public const string JobFailed = "job_failed";
    public const string Cancelled = "cancelled";
    public const string InternalError = "internal_error";
}
