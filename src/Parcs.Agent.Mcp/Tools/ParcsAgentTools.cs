using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;

namespace Parcs.Agent.Mcp.Tools;

/// <summary>
/// MCP tool set that exposes PARCS distributed parallel compute to AI agents.
///
/// A computation is a sequence of LAYERS; each layer runs one IAgentComputation class of a
/// compiled session on N daemon workers. No call blocks longer than its waitSeconds: a layer
/// that is still running comes back as status "Running" with its layerId, and wait_layer picks
/// it up. Every tool returns the same envelope { ok, status, sessionId?, layerId?, layer?,
/// output?, outputs?, workers?, error? } as structured content and as JSON text.
/// </summary>
[McpServerToolType]
public sealed class ParcsAgentTools
{
    private const int DefaultWaitSeconds = 60;
    private const int MaxWaitSeconds     = 200;

    private const string EnvelopeDoc =
        "Every PARCS tool returns { ok, status, sessionId?, layerId?, layerKey?, layer?, output?, outputs?, workers?, error? }; " +
        "ok is false exactly when error = { code, message, retryable, layerId?, failedWorkers?, diagnostics? } is set. ";

    private const string LayerResultDoc =
        "Layer result: status is Queued | Running (ok, call wait_layer) | Completed | CompletedWithFailures | Failed | Cancelled. " +
        "layer = { entryPoint, parallelism, previousLayerIds, inherit?, peerChannels?, successCount, failureCount, queuedSeconds, " +
        "elapsedSeconds, submittedAt, startedAt, completedAt, jobId }. When parallelism is 1, output is that worker's output (parsed when it " +
        "is JSON). view 'outputs' adds outputs[] (one per worker, null for failed ones); view 'full' also adds workers[] = " +
        "{ workerIndex, success, output, error, elapsedSeconds, peers? (workers it had a peer channel with) }. Error codes: invalid_argument, session_not_found, " +
        "layer_not_found, layer_key_conflict, previous_layer_not_ready, previous_layer_failed, capacity_exceeded, not_ready, " +
        "worker_error, out_of_memory, daemon_timeout, no_daemons, host_unavailable, job_failed, cancelled.";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SessionManager           _sessions;
    private readonly IClusterInfoService      _clusterInfo;
    private readonly CapacityGate             _gate;
    private readonly ILogger<ParcsAgentTools> _logger;

    public ParcsAgentTools(
        SessionManager           sessions,
        IClusterInfoService      clusterInfo,
        CapacityGate             gate,
        ILogger<ParcsAgentTools> logger)
    {
        _sessions    = sessions;
        _clusterInfo = clusterInfo;
        _gate        = gate;
        _logger      = logger;
    }

    [McpServerTool(Name = "get_cluster_info", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Returns cluster capacity in output = { scalingMode: 'elastic'|'static', workerNodeCount, currentCapacity, " +
        "maxParallelism, daemonCpuRequestMillicores, capacitySource: 'kubernetes'|'dns'|'configuration'|'fallback', " +
        "freeWorkers, runningLayers, queuedLayers }. Call once at the start to size parallelism. Never request more than " +
        "maxParallelism. In elastic mode, workers above currentCapacity need new nodes (minutes of start-up); in static " +
        "mode a fixed pool serves all layers and nothing is provisioned. Layers that do not fit in freeWorkers wait in a " +
        "server-side queue. capacitySource 'fallback' means the numbers are a configured guess.")]
    public async Task<CallToolResult> GetClusterInfoAsync(CancellationToken ct)
    {
        var info = await _clusterInfo.GetClusterInfoAsync(ct);
        _gate.SetCapacity(info.MaxParallelism);
        return Respond(new ToolEnvelope
        {
            Ok = true,
            Output = JsonSerializer.SerializeToNode(new
            {
                scalingMode                = info.ScalingMode,
                workerNodeCount            = info.WorkerNodeCount,
                currentCapacity            = info.CurrentCapacity,
                maxParallelism             = info.MaxParallelism,
                daemonCpuRequestMillicores = info.DaemonCpuRequestMillicores,
                capacitySource             = info.CapacitySource,
                freeWorkers                = Math.Max(0, info.MaxParallelism - _gate.InUse),
                runningLayers              = _sessions.CountLayers(LayerStatus.Running),
                queuedLayers               = _sessions.CountLayers(LayerStatus.Queued),
            }, JsonOptions),
        });
    }

    [McpServerTool(Name = "create_session", Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Compiles C# source with one or more classes implementing IAgentComputation from Parcs.Agent.Runtime:\n" +
        "  public interface IAgentComputation { Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct); }\n" +
        "AgentLayerInput: WorkerIndex, TotalWorkers, SessionId, LayerId, CustomData (same for every worker), WorkerInput " +
        "(workerInputs[WorkerIndex] of run_layer), Parameters (Dictionary<string,string>), PreviousLayerResults (one JSON " +
        "document per parent layer), PreviousLayerResultJson (= the first of them) and GetPreviousOutputs(parentIndex = 0), " +
        "which returns the parent's per-worker outputs in worker order (null for failed workers), InheritedOutputs (for each " +
        "parent, the output of its worker with this WorkerIndex), and Peer(j) -> IAgentChannel, a direct channel to worker " +
        "j opened the first time either side asks: { SendAsync(string), ReceiveAsync() } plus the extensions " +
        "SendJsonAsync<T>, ReceiveJsonAsync<T>, ExchangeJsonAsync<T>. " + PeersDoc + " Return " +
        "AgentLayerResult.Ok(outputJson) or AgentLayerResult.Error(message). Workers have no network or file access " +
        "besides their peer channels.\n" +
        "Returns status 'Created' or 'Reused' (the same source returns the same session), sessionId and output = " +
        "{ entryPoints: [full class names] }; pass one as run_layer's entryPoint when there are several. On compile " +
        "failure: error.code 'compile_error' with diagnostics [{ id, line, column, message }].")]
    public CallToolResult CreateSession(
        [Description("Complete C# source with one or more IAgentComputation classes, or just the body of ExecuteAsync (usings and a class wrapper are then added).")]
        string sourceCode)
    {
        try
        {
            var (session, reused) = _sessions.CreateSession(sourceCode);
            return Respond(new ToolEnvelope
            {
                Ok        = true,
                Status    = reused ? "Reused" : "Created",
                SessionId = session.SessionId,
                Output    = new JsonObject { ["entryPoints"] = new JsonArray([.. session.EntryPoints.Select(e => JsonValue.Create(e))]) },
            });
        }
        catch (CompilationFailedException ex)
        {
            return Respond(ToolEnvelope.Fail(new ToolError
            {
                Code = ErrorCodes.CompileError, Message = ex.Message, Diagnostics = ex.Diagnostics,
            }));
        }
    }

    [McpServerTool(Name = "run_layer", UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Runs a session's computation on 'parallelism' workers and waits up to waitSeconds (default 60, max 200) for it " +
        "to finish. If it is still running, returns status 'Running' with layerId: call wait_layer(layerId). Pass a " +
        "layerKey to make the call idempotent: repeating it with the same key and arguments returns the same layer " +
        "instead of starting another job, with reused: true (a key whose layer Failed or was Cancelled starts a new attempt). Chain layers " +
        "with previousLayerIds; workers read the parents' results through input.PreviousLayerResults, or only their own " +
        "share with inherit 'byIndex'. All workers of a layer run at once and can talk to each other directly through " +
        "input.Peer(j): the code builds whatever topology it needs as it runs (a ring, a tree, a hypercube, a grid, " +
        "partners chosen from the data) instead of a layer per exchange. " + LayerResultDoc)]
    public async Task<CallToolResult> RunLayerAsync(
        [Description("Session ID from create_session.")] string sessionId,
        [Description("Number of workers; at most maxParallelism from get_cluster_info.")] int parallelism,
        [Description("Seconds to wait for completion before returning status 'Running' (0–200, default 60).")] int? waitSeconds = null,
        [Description("Caller-chosen idempotency key, unique per logical layer within the session, e.g. 'map-3'.")] string? layerKey = null,
        [Description("Full or simple name of the IAgentComputation class to run; required when the session has several.")] string? entryPoint = null,
        [Description("layerIds of completed parent layers, in the order workers see them in input.PreviousLayerResults.")] string[]? previousLayerIds = null,
        [Description("Small string sent unchanged to every worker as input.CustomData.")] string? customData = null,
        [Description("Named parameters sent unchanged to every worker as input.Parameters.")] Dictionary<string, string>? parameters = null,
        [Description("One string per worker (exactly 'parallelism' entries); worker i reads workerInputs[i] as input.WorkerInput.")] string?[]? workerInputs = null,
        [Description("If true, the first failed worker fails the whole layer (status Failed) instead of CompletedWithFailures.")] bool failFast = false,
        [Description("Seconds a peer channel receive waits before throwing TimeoutException (1–900, default 60); also bounds opening a channel.")] int? channelTimeoutSeconds = null,
        [Description("'all' (default): every worker gets every parent's whole result. 'byIndex': worker i gets only output i of each parent, in input.InheritedOutputs (parents must have the same parallelism).")] string? inherit = null,
        [Description("'summary' (default), 'outputs' or 'full'.")] string? view = null,
        CancellationToken ct = default)
    {
        var request = new LayerRequest
        {
            SessionId             = sessionId,
            Parallelism           = parallelism,
            LayerKey              = layerKey,
            EntryPoint            = entryPoint,
            PreviousLayerIds      = previousLayerIds ?? [],
            CustomData            = customData,
            Parameters            = parameters ?? new(),
            WorkerInputs          = workerInputs,
            FailFast              = failFast,
            ChannelTimeoutSeconds = channelTimeoutSeconds,
            Inherit               = inherit,
        };
        return await StartAndWaitAsync(request, ClampWait(waitSeconds ?? DefaultWaitSeconds), view, ct);
    }

    private const string PeersDoc =
        "No topology is declared: it is whatever channels the code opens. Either end may open a channel first; " +
        "Peers.AcceptAsync() returns the next channel another worker opened to this one unasked, and Peers.Connected " +
        "lists the workers connected so far. Sends never block (no deadlock when both sides send first); a receive " +
        "throws TimeoutException after channelTimeoutSeconds and IOException when the other worker failed. Pass " +
        "failFast: true when workers wait on each other.";

    [McpServerTool(Name = "submit_layer", UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Same as run_layer with waitSeconds 0: starts the layer and returns at once with status 'Queued' or 'Running' " +
        "and layerId (or the finished layer when layerKey names one). Use wait_layer to wait for it. " + LayerResultDoc)]
    public async Task<CallToolResult> SubmitLayerAsync(
        [Description("Session ID from create_session.")] string sessionId,
        [Description("Number of workers; at most maxParallelism from get_cluster_info.")] int parallelism,
        [Description("Caller-chosen idempotency key, see run_layer.")] string? layerKey = null,
        [Description("IAgentComputation class to run, see run_layer.")] string? entryPoint = null,
        [Description("layerIds of completed parent layers.")] string[]? previousLayerIds = null,
        [Description("Small string sent to every worker as input.CustomData.")] string? customData = null,
        [Description("Named parameters sent to every worker as input.Parameters.")] Dictionary<string, string>? parameters = null,
        [Description("One string per worker, read as input.WorkerInput.")] string?[]? workerInputs = null,
        [Description("Fail the whole layer on the first failed worker.")] bool failFast = false,
        [Description("Receive timeout of peer channels, see run_layer.")] int? channelTimeoutSeconds = null,
        [Description("'all' (default) or 'byIndex', see run_layer.")] string? inherit = null,
        CancellationToken ct = default)
    {
        var request = new LayerRequest
        {
            SessionId             = sessionId,
            Parallelism           = parallelism,
            LayerKey              = layerKey,
            EntryPoint            = entryPoint,
            PreviousLayerIds      = previousLayerIds ?? [],
            CustomData            = customData,
            Parameters            = parameters ?? new(),
            WorkerInputs          = workerInputs,
            FailFast              = failFast,
            ChannelTimeoutSeconds = channelTimeoutSeconds,
            Inherit               = inherit,
        };
        return await StartAndWaitAsync(request, TimeSpan.Zero, null, ct);
    }

    [McpServerTool(Name = "wait_layer", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Waits up to waitSeconds (default 60, max 200) for a layer to finish and returns it; returns at once if it " +
        "already has. Status 'Running' or 'Queued' means call wait_layer again. " + LayerResultDoc)]
    public async Task<CallToolResult> WaitLayerAsync(
        [Description("layerId from run_layer, submit_layer or list_layers.")] string layerId,
        [Description("Seconds to wait (0–200, default 60).")] int? waitSeconds = null,
        [Description("'summary' (default), 'outputs' or 'full'.")] string? view = null,
        CancellationToken ct = default)
    {
        var layer = _sessions.GetLayer(layerId);
        if (layer is null)
            return Respond(LayerNotFound(layerId));

        await _sessions.WaitAsync(layer, ClampWait(waitSeconds ?? DefaultWaitSeconds), ct);
        return Respond(LayerResponses.Build(layer, view));
    }

    [McpServerTool(Name = "get_layer_result", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc + "Returns a layer's current state without waiting. " + LayerResultDoc)]
    public CallToolResult GetLayerResult(
        [Description("layerId from run_layer, submit_layer or list_layers.")] string layerId,
        [Description("'summary' (default), 'outputs' or 'full'.")] string? view = null)
    {
        var layer = _sessions.GetLayer(layerId);
        return Respond(layer is null ? LayerNotFound(layerId) : LayerResponses.Build(layer, view));
    }

    [McpServerTool(Name = "list_sessions", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Lists sessions newest first in output = { sessions: [{ sessionId, createdAt, entryPoints, layers: [{ layerId, " +
        "layerKey, status, submittedAt }] }] }. Useful for resuming after a connection interruption.")]
    public CallToolResult ListSessions() => Respond(new ToolEnvelope
    {
        Ok = true,
        Output = JsonSerializer.SerializeToNode(new
        {
            sessions = _sessions.ListSessions().Select(s => new
            {
                sessionId   = s.SessionId,
                createdAt   = s.CreatedAt,
                entryPoints = s.EntryPoints,
                layers      = _sessions.ListLayers(s.SessionId).Select(l => new
                {
                    layerId     = l.LayerId,
                    layerKey    = l.LayerKey,
                    status      = l.Status.ToString(),
                    submittedAt = l.SubmittedAt,
                }),
            }),
        }, JsonOptions),
    });

    [McpServerTool(Name = "list_layers", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Lists a session's layers in submission order in output = { layers: [{ layerId, layerKey, status, entryPoint, " +
        "parallelism, previousLayerIds, submittedAt, completedAt }] }. Recovers layerIds lost to a timeout or a dropped connection.")]
    public CallToolResult ListLayers([Description("Session ID from create_session.")] string sessionId)
    {
        if (_sessions.GetSession(sessionId) is null)
        {
            return Respond(ToolEnvelope.Fail(new ToolError
            {
                Code = ErrorCodes.SessionNotFound, Message = $"Session '{sessionId}' not found.",
            }));
        }

        return Respond(new ToolEnvelope
        {
            Ok        = true,
            SessionId = sessionId,
            Output    = JsonSerializer.SerializeToNode(new
            {
                layers = _sessions.ListLayers(sessionId).Select(l => new
                {
                    layerId          = l.LayerId,
                    layerKey         = l.LayerKey,
                    status           = l.Status.ToString(),
                    entryPoint       = l.EntryPoint,
                    parallelism      = l.Parallelism,
                    previousLayerIds = l.PreviousLayerIds,
                    submittedAt      = l.SubmittedAt,
                    completedAt      = l.CompletedAt,
                }),
            }, JsonOptions),
        });
    }

    [McpServerTool(Name = "delete_session", Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Deletes a session and all its layers, cancelling the ones still queued or running. Sessions and finished layers " +
        "are otherwise kept, also across server restarts. Returns status 'Deleted' with output = { deletedLayers }, or " +
        "error.code 'session_not_found'.")]
    public async Task<CallToolResult> DeleteSessionAsync(
        [Description("Session ID from create_session or list_sessions.")] string sessionId,
        CancellationToken ct = default)
    {
        var deleted = await _sessions.DeleteSessionAsync(sessionId, ct);
        if (deleted is null)
        {
            return Respond(ToolEnvelope.Fail(new ToolError
            {
                Code = ErrorCodes.SessionNotFound, Message = $"Session '{sessionId}' not found.",
            }));
        }

        return Respond(new ToolEnvelope
        {
            Ok        = true,
            Status    = "Deleted",
            SessionId = sessionId,
            Output    = new JsonObject { ["deletedLayers"] = deleted.Value },
        });
    }

    [McpServerTool(Name = "cancel_layer", Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolEnvelope))]
    [Description(EnvelopeDoc +
        "Cancels a Queued or Running layer and frees its workers; returns the layer (status Cancelled once the " +
        "cancellation lands). A finished layer is returned unchanged. " + LayerResultDoc)]
    public async Task<CallToolResult> CancelLayerAsync(
        [Description("layerId to cancel.")] string layerId,
        CancellationToken ct = default)
    {
        var layer = _sessions.GetLayer(layerId);
        if (layer is null)
            return Respond(LayerNotFound(layerId));

        await _sessions.CancelAsync(layer, ct);
        return Respond(LayerResponses.Build(layer, null));
    }

    private async Task<CallToolResult> StartAndWaitAsync(LayerRequest request, TimeSpan wait, string? view, CancellationToken ct)
    {
        var started = await _sessions.StartLayerAsync(request, ct);
        if (started.Error is not null)
            return Respond(ToolEnvelope.Fail(started.Error));

        await _sessions.WaitAsync(started.Layer!, wait, ct);
        return Respond(LayerResponses.Build(started.Layer!, view, reused: started.Reused));
    }

    private static TimeSpan ClampWait(int seconds) => TimeSpan.FromSeconds(Math.Clamp(seconds, 0, MaxWaitSeconds));

    private static ToolEnvelope LayerNotFound(string layerId) => ToolEnvelope.Fail(new ToolError
    {
        Code = ErrorCodes.LayerNotFound, Message = $"Layer '{layerId}' not found.",
    });

    internal static CallToolResult Respond(ToolEnvelope envelope)
    {
        var node = JsonSerializer.SerializeToNode(envelope, JsonOptions)!;
        return new CallToolResult
        {
            IsError           = !envelope.Ok,
            Content           = [new TextContentBlock { Text = node.ToJsonString(JsonOptions) }],
            StructuredContent = JsonSerializer.SerializeToElement(node, JsonOptions),
        };
    }
}
