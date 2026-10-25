namespace Parcs.Agent.Mcp.Models;

public enum LayerStatus { Queued, Running, Completed, CompletedWithFailures, Failed, Cancelled }

/// <summary>
/// One parallel execution of session code. Mutable state changes only under <see cref="Sync"/>;
/// <see cref="Completion"/> fires once the layer reaches a terminal status.
/// </summary>
public sealed class LayerRecord
{
    public string LayerId { get; init; } = Guid.NewGuid().ToString("N");

    public required string SessionId { get; init; }

    /// <summary>Caller-chosen key; a repeated key within a session returns this layer.</summary>
    public string? LayerKey { get; init; }

    /// <summary>Hash of the request arguments, to reject a reused key with different arguments.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Full name of the IAgentComputation class that runs, or null for the only one.</summary>
    public string? EntryPoint { get; init; }

    public required int Parallelism { get; init; }

    public IReadOnlyList<string> PreviousLayerIds { get; init; } = [];

    public bool FailFast { get; init; }

    public int ChannelTimeoutSeconds { get; init; } = 60;

    /// <summary>"all" or "byIndex".</summary>
    public string Inherit { get; init; } = "all";

    /// <summary>Points the layer may use in all: its workers plus every point they spawn.</summary>
    public int MaxPoints { get; init; }

    /// <summary>How deep spawned points may nest below the workers (0: no spawning).</summary>
    public int MaxDepth { get; init; }

    /// <summary>Capacity slots the layer holds while it runs.</summary>
    public int Slots { get; init; }

    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;

    public LayerStatus Status { get; set; } = LayerStatus.Queued;

    /// <summary>PARCS job that executes the layer, once it has been created.</summary>
    public long? JobId { get; set; }

    /// <summary>agent_results.json of the job: { sessionId, layerId, totalElapsedSeconds, results: [...] }.</summary>
    public string? ResultJson { get; set; }

    public ToolError? Error { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsTerminal => Status is LayerStatus.Completed or LayerStatus.CompletedWithFailures
                                     or LayerStatus.Failed or LayerStatus.Cancelled;

    /// <summary>A layer that produced results the next layer can read.</summary>
    public bool HasResults => Status is LayerStatus.Completed or LayerStatus.CompletedWithFailures;

    /// <summary>Set by cancel_layer, so a job created after the request is cancelled too.</summary>
    internal bool CancelRequested { get; set; }

    internal Lock Sync { get; } = new();

    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationTokenSource Cancellation { get; } = new();
}
