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
///   3. Repeat run_layer for each subsequent layer, passing previousLayerId
///      from the last completed layer so workers can access prior results.
///
/// ── Error recovery ──────────────────────────────────────────────────────────
/// If a layer fails, call create_session again with corrected code and re-run
/// from the last successful layerId — no earlier work is lost.
/// </summary>
[McpServerToolType]
public sealed class ParcsAgentTools
{
    private readonly SessionManager           _sessions;
    private readonly ClusterInfoService       _clusterInfo;
    private readonly IHttpClientFactory       _httpClientFactory;
    private readonly ILogger<ParcsAgentTools> _logger;

    // Maps datasetUrl → local NFS path; avoids repeated downloads.
    private static readonly ConcurrentDictionary<string, string> _datasetCache = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
        }, _jsonOptions);
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
        "  • PreviousLayerResultJson – full JSON output of the previous layer (populated " +
        "    automatically when you pass previousLayerId to run_layer)\n" +
        "  • CustomData          – shared payload broadcast to all workers\n" +
        "  • Parameters          – Dictionary<string,string> of named parameters\n" +
        "  • DatasetPath         – path to the dataset file on shared NFS storage " +
        "(populated when datasetUrl is passed to run_layer)\n\n" +
        "Return AgentLayerResult.Ok(outputJson) or AgentLayerResult.Error(message).\n\n" +
        "Returns { sessionId } on success or { error } with diagnostics on compile failure. " +
        "On failure, fix the code and call create_session again — no state is lost.")]
    public string CreateSession(
        [Description("Complete C# class implementing IAgentComputation, or just the ExecuteAsync method body (usings and class wrapper are added automatically).")]
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
            }, _jsonOptions);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Compilation failed"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: run_layer  (synchronous — the primary execution tool)
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "run_layer")]
    [Description(
        "Executes the compiled session code across 'parallelism' daemon workers in parallel, " +
        "waits for all workers to finish, and returns aggregated results.\n\n" +
        "To chain layers, pass the layerId returned by the previous run_layer call as " +
        "previousLayerId — the server fetches the stored result automatically and makes it " +
        "available to every worker via input.PreviousLayerResultJson. You never need to " +
        "read or repeat the full result JSON.\n\n" +
        "Returns on success:\n" +
        "  { layerId, status:'Completed', totalElapsedSeconds, successCount, failureCount,\n" +
        "    results: [ { workerIndex, success, outputData, errorMessage, elapsedSeconds } ] }\n\n" +
        "On status:'Failed', check errorMessage. If workers ran out of memory, reduce " +
        "parallelism or simplify per-worker work size and call run_layer again. " +
        "If the C# code threw, fix it with create_session and retry from the last good layerId.")]
    public async Task<string> RunLayerAsync(
        [Description("Session ID from create_session.")]
        string sessionId,

        [Description("Number of parallel workers. Must not exceed maxParallelism from get_cluster_info.")]
        int parallelism,

        [Description("layerId from the previous run_layer call. The server retrieves the stored " +
                     "result and passes it to every worker as input.PreviousLayerResultJson. " +
                     "Omit for the first layer.")]
        string? previousLayerId = null,

        [Description("Optional shared string payload sent unchanged to every worker via input.CustomData. " +
                     "Use this to broadcast a small configuration value (e.g. a threshold, a mode flag, " +
                     "or a compact serialised object) that all workers need identically. " +
                     "For worker-specific inputs, use parameters instead. Maximum a few KB.")]
        string? customData = null,

        [Description("Optional named parameters available to every worker via input.Parameters " +
                     "(a Dictionary<string,string>). Pass as a JSON object: {\"key\": \"value\"}. " +
                     "Workers read values with input.Parameters[\"key\"].")]
        Dictionary<string, string>? parameters = null,

        [Description(
            "Optional URL of a dataset file. Downloaded once by the MCP server to shared cluster " +
            "storage; all workers read it from the same NFS path via input.DatasetPath. " +
            "Supports HuggingFace raw URLs, GCS, Azure Blob, or any public HTTPS URL. " +
            "The file is cached by URL so repeated calls with the same URL skip the download. " +
            "Workers read it with File.ReadAllBytes(input.DatasetPath!) or File.ReadAllText(...). " +
            "Pass null when workers generate their own data from a seed.")]
        string? datasetUrl = null,

        CancellationToken ct = default)
    {
        if (_sessions.GetSession(sessionId) is null)
            return Err($"Session '{sessionId}' not found.");

        if (parallelism < 1 || parallelism > 1000)
            return Err("parallelism must be between 1 and 1000.");

        // Resolve previousLayerResultJson from stored layer — avoids sending huge JSON over the wire.
        string? previousLayerResultJson = null;
        if (!string.IsNullOrWhiteSpace(previousLayerId))
        {
            var prevLayer = _sessions.GetLayer(previousLayerId);
            if (prevLayer is null)
                return Err($"previousLayerId '{previousLayerId}' not found. Use the layerId returned by the previous run_layer call.");
            if (prevLayer.Status != LayerStatus.Completed)
                return Err($"previousLayerId '{previousLayerId}' has status '{prevLayer.Status}' — only Completed layers can be referenced.");
            previousLayerResultJson = prevLayer.ResultJson;
        }

        string? datasetPath = null;
        if (!string.IsNullOrWhiteSpace(datasetUrl) && datasetUrl != "null")
        {
            datasetPath = await FetchDatasetAsync(datasetUrl, ct);
            if (datasetPath is null)
                return Err($"Failed to download dataset from '{datasetUrl}'.");
        }

        _logger.LogInformation(
            "run_layer — session={Session} parallelism={P} prevLayer={Prev} dataset={Url}",
            sessionId, parallelism, previousLayerId ?? "none", datasetUrl ?? "none");

        var layer = await _sessions.RunLayerSyncAsync(
            sessionId, parallelism, previousLayerResultJson,
            customData, parameters ?? new(), datasetPath, ct);

        return BuildLayerResponse(layer);
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_layer_result
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_layer_result")]
    [Description(
        "Returns the stored result of a previously executed layer by its layerId.\n\n" +
        "Primary use cases:\n" +
        "  1. Recovery after connection drop: if run_layer returns status 'Running' " +
        "(the SSE connection was interrupted before the job finished), call this tool " +
        "after a short wait to retrieve the result once the cluster has completed it.\n" +
        "  2. Lazy reads: retrieve a completed layer's result on demand without re-running.\n\n" +
        "Returns { layerId, status:'Completed', result } on success, " +
        "{ layerId, status:'Failed', errorMessage } on failure, or " +
        "{ layerId, status:'Running' } if the layer is still executing.")]
    public string GetLayerResult(
        [Description("layerId returned by run_layer.")]
        string layerId)
    {
        var layer = _sessions.GetLayer(layerId);
        if (layer is null)
            return Err($"Layer '{layerId}' not found.");

        return BuildLayerResponse(layer);
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
                sessionId = s.SessionId,
                createdAt = s.CreatedAt,
            }),
        }, _jsonOptions);
    }

    // ─────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────

    private const string DatasetsRoot = "/var/lib/storage/Datasets";

    private string Err(string message) =>
        JsonSerializer.Serialize(new { error = message }, _jsonOptions);

    /// <summary>
    /// Serialises a completed or failed layer into the tool response.
    /// resultJson is parsed back to a JsonElement so it embeds as a real nested object
    /// rather than an escaped string — avoids Unicode-escaped quotes and saves LLM tokens.
    /// </summary>
    private string BuildLayerResponse(LayerRecord layer)
    {
        if (layer.Status == LayerStatus.Running)
        {
            return JsonSerializer.Serialize(new
            {
                layerId   = layer.LayerId,
                sessionId = layer.SessionId,
                status    = "Running",
                message   = $"The SSE connection was interrupted before this layer finished. " +
                            $"The job is still executing on the cluster. " +
                            $"Call get_layer_result(\"{layer.LayerId}\") in a few seconds to retrieve the result.",
            }, _jsonOptions);
        }

        if (layer.Status == LayerStatus.Failed)
        {
            var errMsg = layer.ErrorMessage ?? "unknown error";
            var hint   = errMsg.Contains("OutOfMemory", StringComparison.OrdinalIgnoreCase) ||
                         errMsg.Contains("out of memory", StringComparison.OrdinalIgnoreCase)
                ? " Try reducing parallelism or the per-worker data volume."
                : string.Empty;

            return JsonSerializer.Serialize(new
            {
                layerId      = layer.LayerId,
                sessionId    = layer.SessionId,
                status       = "Failed",
                errorMessage = errMsg + hint,
            }, _jsonOptions);
        }

        // Parse stored resultJson back to a JsonElement so it serialises
        // as a real nested object (no " Unicode escaping of inner quotes).
        JsonElement? result = null;
        int successCount = 0, failureCount = 0;
        double totalElapsed = 0;

        if (layer.ResultJson is not null)
        {
            try
            {
                var doc = JsonDocument.Parse(layer.ResultJson);
                result = doc.RootElement.Clone();

                // Extract summary stats directly from the parsed result.
                if (doc.RootElement.TryGetProperty("TotalElapsedSeconds", out var te) ||
                    doc.RootElement.TryGetProperty("totalElapsedSeconds", out te))
                    totalElapsed = te.GetDouble();

                if (doc.RootElement.TryGetProperty("Results", out var results) ||
                    doc.RootElement.TryGetProperty("results", out results))
                {
                    foreach (var r in results.EnumerateArray())
                    {
                        var success = (r.TryGetProperty("Success", out var s) ||
                                      r.TryGetProperty("success", out s)) && s.GetBoolean();
                        if (success) successCount++; else failureCount++;
                    }
                }
            }
            catch
            {
                // If parsing fails, fall back to returning the raw string.
            }
        }

        return JsonSerializer.Serialize(new
        {
            layerId             = layer.LayerId,
            sessionId           = layer.SessionId,
            status              = layer.Status.ToString(),
            submittedAt         = layer.SubmittedAt,
            completedAt         = layer.CompletedAt,
            totalElapsedSeconds = totalElapsed,
            successCount,
            failureCount,
            result,   // full nested object — not a string
        }, _jsonOptions);
    }

    private async Task<string?> FetchDatasetAsync(string url, CancellationToken ct)
    {
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
            using var http = _httpClientFactory.CreateClient();
            var bytes      = await http.GetByteArrayAsync(url, ct);
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
}
