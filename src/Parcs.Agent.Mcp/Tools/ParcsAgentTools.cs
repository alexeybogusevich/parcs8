using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;

namespace Parcs.Agent.Mcp.Tools;

/// <summary>
/// MCP tool set that exposes PARCS distributed parallel compute to AI agents.
///
/// ── Execution model ─────────────────────────────────────────────────────────
/// An agent's computation is structured as a sequence of LAYERS, where each
/// layer runs C# code in parallel across N daemon workers:
///
///   1. create_session  — compile a C# IAgentComputation class once.
///   2. run_layer       — execute that code on N workers; block until done.
///                        Each worker receives its WorkerIndex, the shared
///                        previousLayerResultJson from the prior layer, and
///                        optional customData / parameters.
///   3. Repeat run_layer for each subsequent statement, threading the
///                        resultJson of each completed layer into the next
///                        call as previousLayerResultJson.
///
/// ── Error recovery ──────────────────────────────────────────────────────────
/// If a layer fails (compile error or runtime exception), the agent calls
/// create_session again with corrected code and re-runs from the last
/// successful layer's resultJson — no earlier work is lost.
///
/// ── Async variant ───────────────────────────────────────────────────────────
/// submit_layer + get_layer_results give non-blocking control for long layers;
/// run_layer is the simple blocking call preferred for most scenarios.
/// </summary>
[McpServerToolType]
public sealed class ParcsAgentTools
{
    private readonly SessionManager           _sessions;
    private readonly ClusterInfoService       _clusterInfo;
    private readonly IHttpClientFactory       _httpClientFactory;
    private readonly ILogger<ParcsAgentTools> _logger;

    // Maps datasetUrl → local path on shared NFS storage so repeated calls skip the download.
    private static readonly ConcurrentDictionary<string, string> _datasetCache = new();

    public ParcsAgentTools(
        SessionManager            sessions,
        ClusterInfoService        clusterInfo,
        IHttpClientFactory        httpClientFactory,
        ILogger<ParcsAgentTools>  logger)
    {
        _sessions          = sessions;
        _clusterInfo       = clusterInfo;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_cluster_info
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_cluster_info")]
    [Description(
        "Returns PARCS cluster capacity: worker node count, maximum simultaneous daemon " +
        "workers (maxParallelism), and per-daemon CPU allocation in millicores. " +
        "Call once at the start to decide how many workers to use per layer. " +
        "KEDA autoscales nodes on demand, so requesting up to maxParallelism workers is safe.")]
    public async Task<string> GetClusterInfoAsync(CancellationToken ct)
    {
        var info = await _clusterInfo.GetClusterInfoAsync(ct);
        return JsonSerializer.Serialize(new
        {
            workerNodeCount            = info.WorkerNodeCount,
            maxParallelism             = info.MaxParallelism,
            daemonCpuRequestMillicores = info.DaemonCpuRequestMillicores,
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: create_session
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "create_session")]
    [Description(
        "Compiles C# source code and registers it as a PARCS compute session. " +
        "The code must implement IAgentComputation from Parcs.Agent.Runtime:\n\n" +
        "  public interface IAgentComputation {\n" +
        "    Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct);\n" +
        "  }\n\n" +
        "AgentLayerInput fields available to each worker:\n" +
        "  • WorkerIndex         – 0-based index of this worker\n" +
        "  • TotalWorkers        – total number of workers in this layer\n" +
        "  • PreviousLayerResultJson – JSON output of the previous run_layer call (or null for first)\n" +
        "  • CustomData          – shared string payload passed to all workers\n" +
        "  • Parameters          – Dictionary<string,string> of named parameters\n\n" +
        "Return AgentLayerResult.Ok(outputJson) or AgentLayerResult.Error(message).\n\n" +
        "Returns { sessionId } on success or { error } on compile failure. " +
        "On compile failure, fix the code and call create_session again — no state is lost.")]
    public string CreateSession(
        [Description("Complete C# class implementing IAgentComputation, or just the ExecuteAsync body (auto-wrapped).")]
        string sourceCode)
    {
        try
        {
            var session = _sessions.CreateSession(sourceCode);
            _logger.LogInformation("Session {Id} created via MCP", session.SessionId);

            return JsonSerializer.Serialize(new
            {
                sessionId = session.SessionId,
                createdAt = session.CreatedAt,
                message   = "Compiled successfully. Use sessionId with run_layer.",
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Compilation failed"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: run_layer  (synchronous — preferred for incremental execution)
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "run_layer")]
    [Description(
        "Executes one statement of the computation: fans out to 'parallelism' daemon workers, " +
        "waits for all to finish, and returns the aggregated results. " +
        "This is the primary tool for incremental agentic execution — call it once per statement.\n\n" +
        "Each worker receives:\n" +
        "  • Its WorkerIndex and TotalWorkers\n" +
        "  • previousLayerResultJson — pass the resultJson from the previous layer here so workers " +
        "    can build on earlier results (this is how multi-layer pipelines maintain state)\n" +
        "  • customData and parameters for any additional inputs\n\n" +
        "Returns on completion:\n" +
        "  { layerId, status:'Completed'|'Failed', resultJson, errorMessage? }\n\n" +
        "resultJson is a LayerOutputDto with fields:\n" +
        "  • sessionId, layerId, totalElapsedSeconds\n" +
        "  • results: [ { workerIndex, success, outputData, errorMessage, elapsedSeconds } ]\n\n" +
        "If status is Failed, fix the code with create_session and call run_layer again, " +
        "passing the last successful layer's resultJson as previousLayerResultJson.")]
    public async Task<string> RunLayerAsync(
        [Description("Session ID from create_session.")]
        string sessionId,
        [Description("Number of parallel workers. Should not exceed maxParallelism from get_cluster_info.")]
        int parallelism,
        [Description("Pass the resultJson from the previous run_layer call to give workers access to prior results. Null for the first layer.")]
        string? previousLayerResultJson = null,
        [Description("Optional shared string payload sent to every worker unchanged.")]
        string? customData = null,
        [Description("Optional JSON object of key/value parameters, e.g. '{\"start\":\"0\",\"end\":\"1000\"}'. Workers access these via input.Parameters.")]
        string? parametersJson = null,
        [Description(
            "Optional URL of a dataset file to download and distribute to every worker as 'dataset.bin'. " +
            "Downloaded once by the MCP server and attached as a PARCS input file — workers read it with " +
            "File.ReadAllBytes(\"dataset.bin\") or File.ReadAllText(\"dataset.bin\"). " +
            "Supports any URL (HuggingFace, GCS, Azure Blob, etc.). " +
            "The file is cached in memory for the lifetime of the MCP server process, so repeated calls " +
            "with the same URL do not re-download. Pass null if workers generate their own data from a seed.")]
        string? datasetUrl = null,
        CancellationToken ct = default)
    {
        if (_sessions.GetSession(sessionId) is null)
            return JsonSerializer.Serialize(new { error = $"Session '{sessionId}' not found." });

        if (parallelism < 1 || parallelism > 1000)
            return JsonSerializer.Serialize(new { error = "parallelism must be between 1 and 1000." });

        var parameters = ParseParameters(parametersJson, out var parseError);
        if (parseError is not null)
            return JsonSerializer.Serialize(new { error = parseError });

        string? datasetPath = null;
        if (!string.IsNullOrWhiteSpace(datasetUrl) && datasetUrl != "null")
        {
            datasetPath = await FetchDatasetAsync(datasetUrl, ct);
            if (datasetPath is null)
                return JsonSerializer.Serialize(new { error = $"Failed to download dataset from '{datasetUrl}'." });
        }

        _logger.LogInformation(
            "run_layer — session={Session} parallelism={P} dataset={Url}",
            sessionId, parallelism, datasetUrl ?? "none");

        var layer = await _sessions.RunLayerSyncAsync(
            sessionId, parallelism, previousLayerResultJson, customData, parameters!, datasetPath, ct);

        return JsonSerializer.Serialize(new
        {
            layerId      = layer.LayerId,
            sessionId    = layer.SessionId,
            status       = layer.Status.ToString(),
            submittedAt  = layer.SubmittedAt,
            completedAt  = layer.CompletedAt,
            resultJson   = layer.Status == LayerStatus.Completed ? layer.ResultJson : null,
            errorMessage = layer.Status == LayerStatus.Failed    ? layer.ErrorMessage : null,
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: submit_layer  (async — fire and poll)
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "submit_layer")]
    [Description(
        "Submits a parallel layer and returns immediately with a layerId. " +
        "Use this when you want to dispatch a long-running layer without blocking, " +
        "then poll with get_layer_results. For most cases prefer run_layer which blocks until done.")]
    public async Task<string> SubmitLayerAsync(
        [Description("Session ID from create_session.")] string sessionId,
        [Description("Number of parallel workers (1..maxParallelism).")] int parallelism,
        [Description("JSON output from a previous run_layer / get_layer_results (resultJson field).")] string? previousLayerResultJson = null,
        [Description("Shared string payload sent to every worker.")] string? customData = null,
        [Description("JSON object of key/value parameters, e.g. '{\"key\":\"value\"}'.")] string? parametersJson = null,
        [Description("Optional URL of a dataset file to download and distribute to every worker as 'dataset.bin'. See run_layer for full description.")]
        string? datasetUrl = null,
        CancellationToken ct = default)
    {
        var session = _sessions.GetSession(sessionId);
        if (session is null)
            return JsonSerializer.Serialize(new { error = $"Session '{sessionId}' not found." });

        if (parallelism < 1 || parallelism > 1000)
            return JsonSerializer.Serialize(new { error = "parallelism must be between 1 and 1000." });

        var parameters = ParseParameters(parametersJson, out var parseError);
        if (parseError is not null)
            return JsonSerializer.Serialize(new { error = parseError });

        string? datasetPath = null;
        if (!string.IsNullOrWhiteSpace(datasetUrl) && datasetUrl != "null")
        {
            datasetPath = await FetchDatasetAsync(datasetUrl, ct);
            if (datasetPath is null)
                return JsonSerializer.Serialize(new { error = $"Failed to download dataset from '{datasetUrl}'." });
        }

        var layer = _sessions.CreateLayer(sessionId);
        _sessions.SubmitLayerBackground(layer, session, parallelism,
            previousLayerResultJson, customData, parameters!, datasetPath, ct);

        _logger.LogInformation(
            "submit_layer — session={Session} parallelism={P} layer={LayerId} dataset={Url}",
            sessionId, parallelism, layer.LayerId, datasetUrl ?? "none");

        return JsonSerializer.Serialize(new
        {
            layerId     = layer.LayerId,
            sessionId,
            parallelism,
            submittedAt = layer.SubmittedAt,
            message     = "Layer queued. Poll get_layer_results with layerId.",
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_layer_results
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_layer_results")]
    [Description(
        "Returns status and results for a layer submitted via submit_layer. " +
        "Status: Pending | Running | Completed | Failed. " +
        "When Completed, resultJson contains LayerOutputDto (see run_layer description). " +
        "When Failed, errorMessage explains what went wrong. " +
        "Poll every 2–5 seconds until status is terminal.")]
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
            resultJson   = layer.Status == LayerStatus.Completed ? layer.ResultJson : null,
            errorMessage = layer.Status == LayerStatus.Failed    ? layer.ErrorMessage : null,
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: list_sessions
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_sessions")]
    [Description(
        "Lists all active compute sessions in this MCP server instance, ordered newest first. " +
        "Useful for resuming a multi-layer pipeline after a connection interruption.")]
    public string ListSessions()
    {
        var sessions = _sessions.ListSessions();

        return JsonSerializer.Serialize(new
        {
            count    = sessions.Count,
            sessions = sessions.Select(s => new
            {
                sessionId   = s.SessionId,
                createdAt   = s.CreatedAt,
                moduleId    = s.ParcsModuleId,
            }),
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────

    // Datasets directory on the shared NFS volume — same mount as daemons use.
    private const string DatasetsRoot = "/var/lib/storage/Datasets";

    /// <summary>
    /// Downloads <paramref name="url"/> to the shared NFS volume and returns the path.
    /// The path is stable per URL (content-addressed by URL hash), so subsequent calls
    /// with the same URL are instant filesystem hits — no re-download, no re-transfer.
    /// Returns null on download failure.
    /// </summary>
    private async Task<string?> FetchDatasetAsync(string url, CancellationToken ct)
    {
        // Stable subdirectory keyed by URL hash — avoids special characters in paths.
        var key  = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                       System.Text.Encoding.UTF8.GetBytes(url)))[..16];
        var dir  = Path.Combine(DatasetsRoot, key);
        var path = Path.Combine(dir, "dataset.bin");

        if (_datasetCache.ContainsKey(url) || File.Exists(path))
        {
            _logger.LogInformation("Dataset already on shared storage: {Path}", path);
            _datasetCache.TryAdd(url, path);
            return path;
        }

        _logger.LogInformation("Downloading dataset from {Url} → {Path}", url, path);
        try
        {
            Directory.CreateDirectory(dir);
            using var http   = _httpClientFactory.CreateClient();
            var bytes        = await http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, bytes, ct);
            _datasetCache[url] = path;
            _logger.LogInformation("Dataset saved: {Path} ({Bytes} bytes)", path, bytes.Length);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download dataset from {Url}", url);
            return null;
        }
    }

    private static Dictionary<string, string>? ParseParameters(string? parametersJson, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(parametersJson))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson) ?? new();
        }
        catch
        {
            error = "parametersJson must be a valid JSON object, e.g. {\"key\":\"value\"}.";
            return null;
        }
    }
}
