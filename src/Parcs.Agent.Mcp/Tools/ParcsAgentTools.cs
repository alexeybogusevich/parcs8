using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Types;
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
        "maxParallelism reflects current cluster capacity, not a hard ceiling — KEDA " +
        "autoscales nodes on demand, so requesting up to maxParallelism workers is safe, " +
        "and the value itself will rise as the cluster scales up.")]
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
        "Example aggregation layer reading the previous layer's output:\n" +
        "  var prev = JsonSerializer.Deserialize<JsonElement>(input.PreviousLayerResultJson!);\n" +
        "  int total = 0;\n" +
        "  foreach (var r in prev.GetProperty(\"results\").EnumerateArray())\n" +
        "      if (r.GetProperty(\"success\").GetBoolean())\n" +
        "          total += JsonSerializer.Deserialize<JsonElement>(r.GetProperty(\"outputData\").GetString()!)\n" +
        "                       .GetProperty(\"count\").GetInt32();\n\n" +
        "Returns { sessionId } on success. On compile failure, the tool result is marked as an " +
        "error (isError) and the content holds the compiler diagnostics — fix the code and call " +
        "create_session again, no state is lost.")]
    public CallToolResponse CreateSession(
        [Description("Complete C# class implementing IAgentComputation, or just the ExecuteAsync method body (usings and class wrapper are added automatically).")]
        string sourceCode)
    {
        try
        {
            var session = _sessions.CreateSession(sourceCode);
            _logger.LogInformation("Session {Id} created via MCP", session.SessionId);

            return Ok(new
            {
                sessionId = session.SessionId,
                createdAt = session.CreatedAt,
                message   = "Compiled successfully. Use sessionId with run_layer.",
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Compilation failed"))
        {
            return Err(ex.Message);
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
        "If the connection drops before this returns, the layer keeps running on the cluster " +
        "server-side — call get_layer_result with the layerId from list_sessions, or prefer " +
        "submit_layer for anything that might run long, since it returns a layerId immediately.\n\n" +
        "Returns on success (all fields camelCase, 'results' always a top-level array):\n" +
        "  { layerId, sessionId, status:'Completed', totalElapsedSeconds, successCount, failureCount,\n" +
        "    results: [ { workerIndex, success, outputData, errorMessage, elapsedSeconds } ] }\n\n" +
        "On failure the tool result is marked as an error (isError); check errorMessage in the " +
        "content. If workers ran out of memory, reduce parallelism or simplify per-worker work " +
        "size and call run_layer again. If the C# code threw, fix it with create_session and " +
        "retry from the last good layerId.")]
    public async Task<CallToolResponse> RunLayerAsync(
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
        var resolved = await ResolveLayerInputsAsync(sessionId, previousLayerId, datasetUrl, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        _logger.LogInformation(
            "run_layer — session={Session} parallelism={P} prevLayer={Prev} dataset={Url}",
            sessionId, parallelism, previousLayerId ?? "none", datasetUrl ?? "none");

        var layer = await _sessions.RunLayerSyncAsync(
            sessionId, parallelism, resolved.PreviousLayerResultJson,
            customData, parameters ?? new(), resolved.DatasetPath, ct);

        return BuildLayerResponse(layer);
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: submit_layer  (non-blocking — returns immediately)
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "submit_layer")]
    [Description(
        "Starts executing the compiled session code across 'parallelism' daemon workers and " +
        "returns immediately with a layerId, without waiting for the layer to finish. Use this " +
        "instead of run_layer when a layer might run for more than a couple of minutes, or when " +
        "you want to avoid holding the connection open. Poll get_layer_result(layerId) every " +
        "few seconds until status is 'Completed' or 'Failed'.\n\n" +
        "Takes the same parameters as run_layer. Returns { layerId, sessionId, status:'Running' }.")]
    public async Task<CallToolResponse> SubmitLayerAsync(
        [Description("Session ID from create_session.")]
        string sessionId,

        [Description("Number of parallel workers. Must not exceed maxParallelism from get_cluster_info.")]
        int parallelism,

        [Description("layerId from a previous run_layer/submit_layer call, to feed its result to " +
                     "every worker as input.PreviousLayerResultJson. Omit for the first layer.")]
        string? previousLayerId = null,

        [Description("Optional shared string payload sent unchanged to every worker via input.CustomData.")]
        string? customData = null,

        [Description("Optional named parameters available to every worker via input.Parameters.")]
        Dictionary<string, string>? parameters = null,

        [Description("Optional URL of a dataset file — see run_layer for details.")]
        string? datasetUrl = null,

        CancellationToken ct = default)
    {
        var session = _sessions.GetSession(sessionId);
        if (session is null)
            return Err($"Session '{sessionId}' not found.");

        var resolved = await ResolveLayerInputsAsync(sessionId, previousLayerId, datasetUrl, ct);
        if (resolved.Error is not null)
            return resolved.Error;

        var layer = _sessions.CreateLayer(sessionId);

        _logger.LogInformation(
            "submit_layer — session={Session} layer={Layer} parallelism={P} prevLayer={Prev}",
            sessionId, layer.LayerId, parallelism, previousLayerId ?? "none");

        // Runs detached from this request's CancellationToken — the layer must keep running
        // even after this call returns and the MCP request completes.
        _sessions.SubmitLayerBackground(
            layer, session, parallelism, resolved.PreviousLayerResultJson,
            customData, parameters ?? new(), resolved.DatasetPath, CancellationToken.None);

        return Ok(new
        {
            layerId   = layer.LayerId,
            sessionId = layer.SessionId,
            status    = "Running",
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool: get_layer_result
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_layer_result")]
    [Description(
        "Returns the stored result of a previously executed layer by its layerId.\n\n" +
        "Primary use cases:\n" +
        "  1. Recovery after connection drop, or polling a submit_layer call: if status is " +
        "'Running', the layer is still executing on the cluster — wait and call again.\n" +
        "  2. Lazy reads: retrieve a completed layer's result on demand without re-running.\n\n" +
        "Returns the same shape as run_layer: { layerId, sessionId, status, ..., results: [...] } " +
        "on completion, { layerId, sessionId, status:'Running' } while still executing, or an " +
        "error tool result (isError) if the layer failed or was not found.")]
    public CallToolResponse GetLayerResult(
        [Description("layerId returned by run_layer or submit_layer.")]
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

    private static CallToolResponse Ok(object payload) => new()
    {
        Content = [new Content { Type = "text", Text = JsonSerializer.Serialize(payload, _jsonOptions) }],
    };

    private static CallToolResponse Err(string message) => new()
    {
        IsError = true,
        Content = [new Content { Type = "text", Text = JsonSerializer.Serialize(new { error = message }, _jsonOptions) }],
    };

    private readonly record struct ResolvedLayerInputs(
        string? PreviousLayerResultJson, string? DatasetPath, CallToolResponse? Error);

    /// <summary>
    /// Resolves previousLayerId → stored result JSON and datasetUrl → local NFS path, shared by
    /// run_layer and submit_layer. Returns an <see cref="ResolvedLayerInputs.Error"/> tool
    /// response instead of throwing, so callers can return it directly.
    /// </summary>
    private async Task<ResolvedLayerInputs> ResolveLayerInputsAsync(
        string sessionId, string? previousLayerId, string? datasetUrl, CancellationToken ct)
    {
        if (_sessions.GetSession(sessionId) is null)
            return new ResolvedLayerInputs(null, null, Err($"Session '{sessionId}' not found."));

        string? previousLayerResultJson = null;
        if (!string.IsNullOrWhiteSpace(previousLayerId))
        {
            var prevLayer = _sessions.GetLayer(previousLayerId);
            if (prevLayer is null)
                return new ResolvedLayerInputs(null, null,
                    Err($"previousLayerId '{previousLayerId}' not found. Use the layerId returned by the previous run_layer/submit_layer call."));
            if (prevLayer.Status != LayerStatus.Completed)
                return new ResolvedLayerInputs(null, null,
                    Err($"previousLayerId '{previousLayerId}' has status '{prevLayer.Status}' — only Completed layers can be referenced."));
            previousLayerResultJson = prevLayer.ResultJson;
        }

        string? datasetPath = null;
        if (!string.IsNullOrWhiteSpace(datasetUrl) && datasetUrl != "null")
        {
            datasetPath = await FetchDatasetAsync(datasetUrl, ct);
            if (datasetPath is null)
                return new ResolvedLayerInputs(null, null, Err($"Failed to download dataset from '{datasetUrl}'."));
        }

        return new ResolvedLayerInputs(previousLayerResultJson, datasetPath, null);
    }

    /// <summary>
    /// Builds the tool response for a layer. resultJson (produced by AgentRunnerMainModule, and
    /// stored verbatim as PreviousLayerResultJson for the next layer) is camelCase and already
    /// shaped as { sessionId, layerId, totalElapsedSeconds, anyFailures, results: [...] } — this
    /// flattens that straight into the top-level response, matching what run_layer documents.
    /// </summary>
    private CallToolResponse BuildLayerResponse(LayerRecord layer)
    {
        if (layer.Status == LayerStatus.Running)
        {
            return Ok(new
            {
                layerId   = layer.LayerId,
                sessionId = layer.SessionId,
                status    = "Running",
                message   = $"The layer is still executing on the cluster " +
                            $"(the SSE connection to the original call may have dropped). " +
                            $"Call get_layer_result(\"{layer.LayerId}\") again in a few seconds.",
            });
        }

        if (layer.Status == LayerStatus.Failed)
        {
            var errMsg = layer.ErrorMessage ?? "unknown error";
            var hint   = errMsg.Contains("OutOfMemory", StringComparison.OrdinalIgnoreCase) ||
                         errMsg.Contains("out of memory", StringComparison.OrdinalIgnoreCase)
                ? " Try reducing parallelism or the per-worker data volume."
                : errMsg.Contains("Timed out", StringComparison.OrdinalIgnoreCase)
                ? " A daemon likely failed to schedule or connect — this is a cluster-side issue, retrying the layer is reasonable."
                : string.Empty;

            return Err($"layerId={layer.LayerId}: {errMsg}{hint}");
        }

        JsonElement? results = null;
        int successCount = 0, failureCount = 0;
        double totalElapsed = 0;

        if (layer.ResultJson is not null)
        {
            try
            {
                var doc = JsonDocument.Parse(layer.ResultJson);

                if (doc.RootElement.TryGetProperty("totalElapsedSeconds", out var te) ||
                    doc.RootElement.TryGetProperty("TotalElapsedSeconds", out te))
                    totalElapsed = te.GetDouble();

                if (doc.RootElement.TryGetProperty("results", out var r) ||
                    doc.RootElement.TryGetProperty("Results", out r))
                {
                    results = r.Clone();
                    foreach (var worker in r.EnumerateArray())
                    {
                        var success = (worker.TryGetProperty("success", out var s) ||
                                       worker.TryGetProperty("Success", out s)) && s.GetBoolean();
                        if (success) successCount++; else failureCount++;
                    }
                }
            }
            catch
            {
                // If parsing fails, fall through with an empty results array rather than
                // returning the whole thing as an opaque, unparsed string.
            }
        }

        return Ok(new
        {
            layerId             = layer.LayerId,
            sessionId           = layer.SessionId,
            status              = layer.Status.ToString(),
            submittedAt         = layer.SubmittedAt,
            completedAt         = layer.CompletedAt,
            totalElapsedSeconds = totalElapsed,
            successCount,
            failureCount,
            results,   // top-level array, matching the tool description — not nested under "result"
        });
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
