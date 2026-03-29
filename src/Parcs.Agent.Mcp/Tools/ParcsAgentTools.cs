using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;

namespace Parcs.Agent.Mcp.Tools;

/// <summary>
/// MCP tool set exposing PARCS parallel compute capabilities to AI agents.
///
/// Workflow:
///   1. get_cluster_info    — discover available parallelism
///   2. create_session      — compile C# code and register it with PARCS
///   3. submit_layer        — dispatch a parallel layer job (non-blocking)
///   4. get_layer_results   — poll until complete, then read results
/// </summary>
[McpServerToolType]
public sealed class ParcsAgentTools
{
    private readonly SessionManager    _sessions;
    private readonly ClusterInfoService _clusterInfo;
    private readonly ILogger<ParcsAgentTools> _logger;

    public ParcsAgentTools(
        SessionManager       sessions,
        ClusterInfoService   clusterInfo,
        ILogger<ParcsAgentTools> logger)
    {
        _sessions    = sessions;
        _clusterInfo = clusterInfo;
        _logger      = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_cluster_info
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_cluster_info")]
    [Description(
        "Returns the current PARCS/Kubernetes cluster capacity: number of worker nodes, " +
        "maximum parallel daemons, and CPU allocation per daemon. " +
        "Call this first to decide how much parallelism to request when submitting layers.")]
    public async Task<string> GetClusterInfoAsync(CancellationToken ct)
    {
        var info = await _clusterInfo.GetClusterInfoAsync(ct);
        return JsonSerializer.Serialize(new
        {
            workerNodeCount            = info.WorkerNodeCount,
            maxParallelism             = info.MaxParallelism,
            daemonCpuRequestMillicores = info.DaemonCpuRequestMillicores,
            notes = "maxParallelism is the cluster-wide ceiling for simultaneous workers. " +
                    "KEDA autoscales nodes on demand so you can request up to maxParallelism workers per layer.",
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: create_session
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "create_session")]
    [Description(
        "Compiles the provided C# source code and registers it as a PARCS compute session. " +
        "The code must contain (or the body of) a class implementing IAgentComputation:\n" +
        "  public interface IAgentComputation {\n" +
        "      Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);\n" +
        "  }\n" +
        "If you only provide a method body, it will be wrapped automatically. " +
        "Returns a sessionId to use with submit_layer.")]
    public string CreateSession(
        [Description("C# source code. Either a complete class file or just the body of ExecuteAsync.")] string sourceCode)
    {
        try
        {
            var session = _sessions.CreateSession(sourceCode);
            _logger.LogInformation("Session {Id} created via MCP", session.SessionId);

            return JsonSerializer.Serialize(new
            {
                sessionId = session.SessionId,
                createdAt = session.CreatedAt,
                message   = "Compilation successful. Use sessionId with submit_layer.",
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Compilation failed"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: submit_layer
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "submit_layer")]
    [Description(
        "Submits a parallel computation layer to PARCS. The layer fans out to 'parallelism' " +
        "daemon workers, each receiving an AgentLayerInput with its WorkerIndex (0..parallelism-1), " +
        "optional previousLayerResultJson, customData, and parameters. " +
        "Returns a layerId immediately. Use get_layer_results to poll for completion.")]
    public string SubmitLayer(
        [Description("Session ID from create_session.")] string sessionId,
        [Description("Number of parallel workers (1..maxParallelism).")] int parallelism,
        [Description("Optional JSON string from a previous layer's output to pass as context.")] string? previousLayerResultJson = null,
        [Description("Arbitrary string payload passed to every worker.")] string? customData = null,
        [Description("JSON-encoded key/value parameters, e.g. '{\"chunkSize\":\"100\"}'.")] string? parametersJson = null,
        CancellationToken ct = default)
    {
        var session = _sessions.GetSession(sessionId);
        if (session is null)
            return JsonSerializer.Serialize(new { error = $"Session '{sessionId}' not found." });

        if (parallelism < 1 || parallelism > 1000)
            return JsonSerializer.Serialize(new { error = "parallelism must be between 1 and 1000." });

        var parameters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson)
                             ?? new();
            }
            catch
            {
                return JsonSerializer.Serialize(new { error = "parametersJson must be a valid JSON object." });
            }
        }

        var layer = _sessions.CreateLayer(sessionId);

        _sessions.SubmitLayerBackground(
            layer, session, parallelism,
            previousLayerResultJson, customData, parameters, ct);

        _logger.LogInformation(
            "Layer {LayerId} submitted — session={Session} parallelism={P}",
            layer.LayerId, sessionId, parallelism);

        return JsonSerializer.Serialize(new
        {
            layerId      = layer.LayerId,
            sessionId,
            parallelism,
            submittedAt  = layer.SubmittedAt,
            message      = "Layer submitted. Poll get_layer_results with layerId to check progress.",
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_layer_results
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_layer_results")]
    [Description(
        "Returns the current status and results for a layer submitted via submit_layer. " +
        "Status values: Pending, Running, Completed, Failed. " +
        "When Completed, resultJson contains a LayerOutputDto with per-worker WorkerResult objects. " +
        "Poll this tool every few seconds until status is Completed or Failed.")]
    public string GetLayerResults(
        [Description("Layer ID returned by submit_layer.")] string layerId)
    {
        var layer = _sessions.GetLayer(layerId);
        if (layer is null)
            return JsonSerializer.Serialize(new { error = $"Layer '{layerId}' not found." });

        return JsonSerializer.Serialize(new
        {
            layerId      = layer.LayerId,
            sessionId    = layer.SessionId,
            status       = layer.Status.ToString(),
            submittedAt  = layer.SubmittedAt,
            completedAt  = layer.CompletedAt,
            resultJson   = layer.Status == LayerStatus.Completed
                               ? layer.ResultJson
                               : null,
            errorMessage = layer.Status == LayerStatus.Failed
                               ? layer.ErrorMessage
                               : null,
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: list_sessions
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_sessions")]
    [Description("Lists all active sessions in the current MCP server instance.")]
    public string ListSessions()
    {
        // The session dictionary is internal — expose it via SessionManager
        return JsonSerializer.Serialize(new { message = "Use get_layer_results with a known layerId." });
    }
}
