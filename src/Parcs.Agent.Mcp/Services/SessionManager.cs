using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Runtime;

namespace Parcs.Agent.Mcp.Services;

/// <summary>Arguments of run_layer / submit_layer after parsing.</summary>
public sealed record LayerRequest
{
    public required string SessionId { get; init; }
    public required int Parallelism { get; init; }
    public IReadOnlyList<string> PreviousLayerIds { get; init; } = [];
    public string? EntryPoint { get; init; }
    public string? CustomData { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string?>? WorkerInputs { get; init; }
    public string? LayerKey { get; init; }

    public bool FailFast { get; init; }

    public int? ChannelTimeoutSeconds { get; init; }

    /// <summary>"all" (default) or "byIndex".</summary>
    public string? Inherit { get; init; }

    /// <summary>Points the layer may use in all, its workers and every point they spawn; null means parallelism (no spawning).</summary>
    public int? MaxPoints { get; init; }

    /// <summary>How deep spawned points may nest below the workers; null means 3 when spawning is on.</summary>
    public int? MaxDepth { get; init; }
}

/// <summary>How a layer receives its parents' results.</summary>
public static class InheritModes
{
    /// <summary>Every worker gets every parent's whole result document.</summary>
    public const string All = "all";

    /// <summary>Worker i gets only output i of each parent (parents must have the same parallelism).</summary>
    public const string ByIndex = "byIndex";
}

/// <summary>The layer that serves a request (new or found by layerKey), or why none does.</summary>
public sealed record StartLayerResult(LayerRecord? Layer, bool Reused, ToolError? Error)
{
    public static StartLayerResult Fail(string code, string message, bool retryable = false) =>
        new(null, false, new ToolError { Code = code, Message = message, Retryable = retryable });
}

/// <summary>
/// Store for sessions and layers, and the executor of layers. Sessions map compiled user code
/// to the AgentRunner module; each layer is one PARCS job that runs in the background, so
/// callers wait on it with a bounded timeout instead of blocking. State lives in memory and is
/// mirrored to an <see cref="ISessionStore"/>, so sessions and finished layers survive restarts.
/// </summary>
public sealed partial class SessionManager
{
    public const int DefaultChannelTimeoutSeconds = 60;
    public const int MaxChannelTimeoutSeconds     = 900;

    // Spawning (SpawnAsync in worker code). In static mode a daemon hosts several points, so a
    // layer may use more points than there are daemons; in elastic mode every point is a pod.
    public const int DefaultSpawnDepth           = 3;
    public const int MaxSpawnDepth               = 8;
    public const int StaticPointsPerWorker       = 4;

    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    private readonly ConcurrentDictionary<string, string>        _sessionsBySource = new();
    private readonly ConcurrentDictionary<string, LayerRecord>   _layers = new();
    private readonly Dictionary<(string SessionId, string Key), string> _layerKeys = new();
    private readonly Lock _layerKeysLock = new();

    private readonly RoslynCompilerService   _compiler;
    private readonly IParcsApiClient         _api;
    private readonly IAgentRunnerModule      _module;
    private readonly IClusterInfoService     _clusterInfo;
    private readonly CapacityGate            _gate;
    private readonly ISessionStore           _store;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(
        RoslynCompilerService   compiler,
        IParcsApiClient         api,
        IAgentRunnerModule      module,
        IClusterInfoService     clusterInfo,
        CapacityGate            gate,
        ILogger<SessionManager> logger,
        ISessionStore?          store = null)
    {
        _compiler    = compiler;
        _api         = api;
        _module      = module;
        _clusterInfo = clusterInfo;
        _gate        = gate;
        _logger      = logger;
        _store       = store ?? NullSessionStore.Instance;

        Restore();
    }

    /// <summary>Reloads stored sessions and layers. A layer that was not finished is lost with its job.</summary>
    private void Restore()
    {
        var (sessions, layers) = _store.Load();
        foreach (var session in sessions)
        {
            _sessions[session.SessionId] = session;
            _sessionsBySource[session.SourceHash] = session.SessionId;
        }

        foreach (var layer in layers.Where(l => _sessions.ContainsKey(l.SessionId)).OrderBy(l => l.SubmittedAt))
        {
            if (!layer.IsTerminal)
            {
                var status = layer.Status;
                Finish(layer, LayerStatus.Failed, new ToolError
                {
                    Code      = ErrorCodes.JobFailed,
                    Message   = $"The MCP server restarted while the layer was {status}; run it again.",
                    Retryable = true,
                    LayerId   = layer.LayerId,
                });
            }

            layer.Completion.TrySetResult();
            _layers[layer.LayerId] = layer;
            if (layer.LayerKey is { } key)
                _layerKeys[(layer.SessionId, key)] = layer.LayerId;
        }

        if (_sessions.Count > 0)
            _logger.LogInformation("Restored {Sessions} sessions and {Layers} layers", _sessions.Count, _layers.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Sessions
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <paramref name="sourceCode"/> into a session. The same source (up to line endings
    /// and surrounding whitespace) returns the existing session with <c>reused = true</c>, so a
    /// script re-run from its start does not create duplicates.
    /// Throws <see cref="CompilationFailedException"/> on compile errors.
    /// </summary>
    public (SessionRecord Session, bool Reused) CreateSession(string sourceCode)
    {
        var sourceHash = Hash(sourceCode.Replace("\r\n", "\n").Trim());
        if (_sessionsBySource.TryGetValue(sourceHash, out var existingId) &&
            _sessions.TryGetValue(existingId, out var existing))
        {
            return (existing, true);
        }

        var compiled = _compiler.Compile(sourceCode);
        var session = new SessionRecord
        {
            SourceCode  = sourceCode,
            SourceHash  = sourceHash,
            EntryPoints = compiled.EntryPoints,
            Assembly    = compiled.Assembly,
        };

        // Two concurrent calls with the same source: the first one registered wins.
        var winnerId = _sessionsBySource.GetOrAdd(sourceHash, session.SessionId);
        if (winnerId != session.SessionId && _sessions.TryGetValue(winnerId, out var winner))
            return (winner, true);

        _sessions[session.SessionId] = session;
        _store.SaveSession(session);
        _logger.LogInformation(
            "Session {Id} created — assembly size={Size} bytes, entry points: {EntryPoints}",
            session.SessionId, session.Assembly.Length, string.Join(", ", session.EntryPoints));
        return (session, false);
    }

    public SessionRecord? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    /// <summary>Returns all sessions ordered by creation time descending.</summary>
    public IReadOnlyList<SessionRecord> ListSessions() =>
        [.. _sessions.Values.OrderByDescending(s => s.CreatedAt)];

    /// <summary>
    /// Removes a session with all its layers, cancelling those still queued or running, and
    /// deletes them from the store. Returns the number of layers removed, or null when there is
    /// no such session.
    /// </summary>
    public async Task<int?> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return null;

        _sessionsBySource.TryRemove(KeyValuePair.Create(session.SourceHash, sessionId));

        var layers = ListLayers(sessionId);
        await Task.WhenAll(layers.Where(l => !l.IsTerminal).Select(l => CancelAsync(l, ct)));

        foreach (var layer in layers)
            _layers.TryRemove(layer.LayerId, out _);

        lock (_layerKeysLock)
        {
            foreach (var key in _layerKeys.Keys.Where(k => k.SessionId == sessionId).ToList())
                _layerKeys.Remove(key);
        }

        _store.DeleteSession(sessionId);
        _logger.LogInformation("Session {Id} deleted with {Count} layers", sessionId, layers.Count);
        return layers.Count;
    }

    // ─────────────────────────────────────────────────────────────────
    // Layers
    // ─────────────────────────────────────────────────────────────────

    public LayerRecord? GetLayer(string layerId) =>
        _layers.TryGetValue(layerId, out var l) ? l : null;

    /// <summary>Returns the layers of a session ordered by submission time ascending.</summary>
    public IReadOnlyList<LayerRecord> ListLayers(string sessionId) =>
        [.. _layers.Values.Where(l => l.SessionId == sessionId).OrderBy(l => l.SubmittedAt)];

    public int CountLayers(LayerStatus status) => _layers.Values.Count(l => l.Status == status);

    /// <summary>
    /// Validates the request and starts the layer in the background, or returns the layer that
    /// already holds <see cref="LayerRequest.LayerKey"/>. A key whose layer Failed or was
    /// Cancelled starts a new attempt under the same key.
    /// </summary>
    public async Task<StartLayerResult> StartLayerAsync(LayerRequest request, CancellationToken ct = default)
    {
        var session = GetSession(request.SessionId);
        if (session is null)
            return StartLayerResult.Fail(ErrorCodes.SessionNotFound, $"Session '{request.SessionId}' not found.");

        if (request.Parallelism < 1)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, "parallelism must be at least 1.");

        var (entryPoint, entryPointError) = ResolveEntryPoint(session, request.EntryPoint);
        if (entryPointError is not null)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, entryPointError);

        if (request.WorkerInputs is { } inputs && inputs.Count != request.Parallelism)
        {
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument,
                $"workerInputs has {inputs.Count} entries; it needs exactly one per worker ({request.Parallelism}).");
        }

        var channelTimeout = request.ChannelTimeoutSeconds ?? DefaultChannelTimeoutSeconds;
        if (channelTimeout < 1 || channelTimeout > MaxChannelTimeoutSeconds)
        {
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument,
                $"channelTimeoutSeconds must be between 1 and {MaxChannelTimeoutSeconds}.");
        }

        string inherit;
        if (string.IsNullOrWhiteSpace(request.Inherit) || request.Inherit.Equals(InheritModes.All, StringComparison.OrdinalIgnoreCase))
            inherit = InheritModes.All;
        else if (request.Inherit.Equals(InheritModes.ByIndex, StringComparison.OrdinalIgnoreCase))
            inherit = InheritModes.ByIndex;
        else
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, $"inherit must be 'all' or 'byIndex', not '{request.Inherit}'.");

        if (inherit == InheritModes.ByIndex && request.PreviousLayerIds.Count == 0)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, "inherit 'byIndex' needs previousLayerIds.");

        var maxPoints = request.MaxPoints ?? request.Parallelism;
        if (maxPoints < request.Parallelism)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument,
                $"maxPoints ({maxPoints}) counts the workers too, so it must be at least parallelism ({request.Parallelism}).");
        var spawning = maxPoints > request.Parallelism;
        var maxDepth = request.MaxDepth ?? (spawning ? DefaultSpawnDepth : 0);
        if (maxDepth < 0 || maxDepth > MaxSpawnDepth)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, $"maxDepth must be between 0 and {MaxSpawnDepth}.");
        if (spawning && maxDepth == 0)
            return StartLayerResult.Fail(ErrorCodes.InvalidArgument, "maxPoints above parallelism lets workers spawn, which needs maxDepth of at least 1.");

        var failFast = request.FailFast;
        var options = new LayerOptions(channelTimeout, inherit, failFast, maxPoints, maxDepth);
        var fingerprint = Fingerprint(request, entryPoint, options);

        // An identical repeat of a keyed call returns the original layer before anything else is
        // checked: replaying a script must not fail because, say, capacity changed since.
        if (request.LayerKey is { Length: > 0 } key)
        {
            var existing = FindKeyedLayer(request.SessionId, key);
            if (existing is not null && existing.Status is not (LayerStatus.Failed or LayerStatus.Cancelled))
            {
                return existing.Fingerprint == fingerprint
                    ? new StartLayerResult(existing, true, null)
                    : StartLayerResult.Fail(ErrorCodes.LayerKeyConflict,
                        $"layerKey '{key}' already names layer {existing.LayerId} with different arguments. " +
                        "Use a new key, or repeat the original arguments to get that layer.");
            }
        }

        var previousResults = new List<string>(request.PreviousLayerIds.Count);
        foreach (var previousId in request.PreviousLayerIds)
        {
            var previous = GetLayer(previousId);
            if (previous is null)
                return StartLayerResult.Fail(ErrorCodes.LayerNotFound, $"previousLayerIds: layer '{previousId}' not found.");

            if (!previous.HasResults)
            {
                return previous.IsTerminal
                    ? StartLayerResult.Fail(ErrorCodes.PreviousLayerFailed,
                        $"previousLayerIds: layer '{previousId}' is {previous.Status} and has no results.")
                    : StartLayerResult.Fail(ErrorCodes.PreviousLayerNotReady,
                        $"previousLayerIds: layer '{previousId}' is still {previous.Status}; wait for it first.", retryable: true);
            }

            if (inherit == InheritModes.ByIndex && previous.Parallelism != request.Parallelism)
            {
                return StartLayerResult.Fail(ErrorCodes.InvalidArgument,
                    $"inherit 'byIndex' gives worker i output i of each parent, so parents need parallelism {request.Parallelism}; " +
                    $"layer '{previousId}' has {previous.Parallelism}. Use inherit 'all' to read every output.");
            }

            previousResults.Add(previous.ResultJson!);
        }

        if (!_module.IsReady)
        {
            return StartLayerResult.Fail(ErrorCodes.NotReady,
                "The AgentRunner module is not yet registered with the PARCS host. Retry in a few seconds.", retryable: true);
        }

        var cluster = await _clusterInfo.GetClusterInfoAsync(ct);
        _gate.SetCapacity(cluster.MaxParallelism);
        if (request.Parallelism > cluster.MaxParallelism)
        {
            return StartLayerResult.Fail(ErrorCodes.CapacityExceeded,
                $"parallelism {request.Parallelism} exceeds maxParallelism {cluster.MaxParallelism} " +
                $"({cluster.ScalingMode} mode, capacity source: {cluster.CapacitySource}).");
        }

        // Spawned points must be schedulable while their parents wait for them. Elastic mode gives
        // every point its own pod, so the whole budget must fit and is reserved up front; in
        // static mode a daemon hosts several points, so the budget may exceed the daemons.
        var pointLimit = cluster.ScalingMode == ScalingModes.Static
            ? cluster.MaxParallelism * StaticPointsPerWorker
            : cluster.MaxParallelism;
        if (maxPoints > pointLimit)
        {
            return StartLayerResult.Fail(ErrorCodes.CapacityExceeded,
                $"maxPoints {maxPoints} exceeds the {pointLimit} points this cluster can run at once " +
                $"({cluster.ScalingMode} mode, maxParallelism {cluster.MaxParallelism}).");
        }
        var slots = Math.Min(maxPoints, cluster.MaxParallelism);

        var layer = new LayerRecord
        {
            SessionId        = session.SessionId,
            LayerKey         = request.LayerKey is { Length: > 0 } ? request.LayerKey : null,
            Fingerprint      = fingerprint,
            EntryPoint       = entryPoint,
            Parallelism      = request.Parallelism,
            PreviousLayerIds = request.PreviousLayerIds,
            FailFast         = failFast,
            ChannelTimeoutSeconds = channelTimeout,
            Inherit          = inherit,
            MaxPoints        = maxPoints,
            MaxDepth         = maxDepth,
            Slots            = slots,
        };

        if (layer.LayerKey is { } layerKey)
        {
            lock (_layerKeysLock)
            {
                // Re-check under the lock: a concurrent call with the same key may have won.
                if (_layerKeys.TryGetValue((layer.SessionId, layerKey), out var winnerId) &&
                    GetLayer(winnerId) is { Status: not (LayerStatus.Failed or LayerStatus.Cancelled) } winner)
                {
                    return winner.Fingerprint == fingerprint
                        ? new StartLayerResult(winner, true, null)
                        : StartLayerResult.Fail(ErrorCodes.LayerKeyConflict,
                            $"layerKey '{layerKey}' already names layer {winner.LayerId} with different arguments.");
                }

                _layers[layer.LayerId] = layer;
                _layerKeys[(layer.SessionId, layerKey)] = layer.LayerId;
            }
        }
        else
        {
            _layers[layer.LayerId] = layer;
        }

        Persist(layer);
        _logger.LogInformation(
            "Layer {LayerId} submitted — session={Session} key={Key} entryPoint={EntryPoint} parallelism={P} parents={Parents}",
            layer.LayerId, layer.SessionId, layer.LayerKey ?? "none", entryPoint ?? "default",
            layer.Parallelism, layer.PreviousLayerIds.Count);

        // Detached from the caller: the layer keeps running after the tool call returns.
        _ = Task.Run(() => ExecuteAsync(layer, session, request, previousResults), CancellationToken.None);

        return new StartLayerResult(layer, false, null);
    }

    /// <summary>Waits up to <paramref name="timeout"/> for the layer to reach a terminal status.</summary>
    public async Task WaitAsync(LayerRecord layer, TimeSpan timeout, CancellationToken ct = default)
    {
        if (layer.IsTerminal || timeout <= TimeSpan.Zero)
            return;

        try
        {
            await layer.Completion.Task.WaitAsync(timeout, ct);
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException) { }   // the client went away; the layer keeps running
    }

    /// <summary>
    /// Cancels a queued or running layer: a queued layer leaves the queue, a running one has its
    /// PARCS job cancelled, which frees its daemons. Waits briefly for the cancellation to land.
    /// </summary>
    public async Task CancelAsync(LayerRecord layer, CancellationToken ct = default)
    {
        long? jobId;
        lock (layer.Sync)
        {
            if (layer.IsTerminal)
                return;
            layer.CancelRequested = true;
            jobId = layer.JobId;
        }

        layer.Cancellation.Cancel();
        if (jobId is { } id)
            await CancelJobQuietlyAsync(id);

        await WaitAsync(layer, TimeSpan.FromSeconds(10), ct);
    }

    // ─────────────────────────────────────────────────────────────────
    // Execution (background task)
    // ─────────────────────────────────────────────────────────────────

    private async Task ExecuteAsync(
        LayerRecord layer, SessionRecord session, LayerRequest request, List<string> previousResults)
    {
        var token = layer.Cancellation.Token;
        IDisposable? lease = null;
        try
        {
            lease = await _gate.AcquireAsync(layer.Slots, token);
            lock (layer.Sync)
            {
                layer.Status    = LayerStatus.Running;
                layer.StartedAt = DateTimeOffset.UtcNow;
            }

            // Per-worker data goes to worker_{i}.json, so each worker reads only its own share
            // instead of every worker reading every input (and, with byIndex, every parent output).
            var byIndex = layer.Inherit == InheritModes.ByIndex;
            var perWorkerFiles = byIndex || request.WorkerInputs is not null;

            // Build layer_input.json — PascalCase to match LayerInputDto property names
            // (System.Text.Json deserialisation is case-sensitive by default).
            var layerInputDto = new
            {
                SessionId             = session.SessionId,
                LayerId               = layer.LayerId,
                TotalWorkers          = layer.Parallelism,
                EntryPoint            = layer.EntryPoint,
                FailFast              = layer.FailFast,
                PreviousLayerResults  = byIndex ? [] : previousResults,
                CustomData            = string.IsNullOrWhiteSpace(request.CustomData) ? null : request.CustomData,
                Parameters            = request.Parameters,
                ChannelTimeoutSeconds = layer.ChannelTimeoutSeconds,
                MaxPoints             = layer.MaxPoints,
                MaxDepth              = layer.MaxDepth,
                PerWorkerFiles        = perWorkerFiles,
                Inherit               = layer.Inherit,
            };
            List<(string Filename, byte[] Bytes)> inputFiles =
            [
                ("agent_computation.dll", session.Assembly),
                ("layer_input.json",      Encoding.UTF8.GetBytes(JsonSerializer.Serialize(layerInputDto))),
            ];

            if (perWorkerFiles)
            {
                var parentOutputs = byIndex
                    ? previousResults.Select(LayerResultDocument.ReadOutputs).ToList()
                    : [];
                for (var i = 0; i < layer.Parallelism; i++)
                {
                    var workerFile = new
                    {
                        WorkerInput      = request.WorkerInputs?[i],
                        InheritedOutputs = parentOutputs.Select(o => i < o.Count ? o[i] : null).ToList(),
                    };
                    inputFiles.Add(($"worker_{i}.json", JsonSerializer.SerializeToUtf8Bytes(workerFile)));
                }
            }

            var jobId = await _api.CreateJobAsync(
                moduleId:     _module.ModuleId,
                assemblyName: _module.AssemblyName,
                className:    _module.ClassName,
                inputFiles:   inputFiles,
                arguments: new Dictionary<string, string>
                {
                    ["PointsNumber"] = layer.Parallelism.ToString(),
                },
                ct: token);

            bool cancelRequested;
            lock (layer.Sync)
            {
                layer.JobId     = jobId;
                cancelRequested = layer.CancelRequested;
            }

            // A cancel that arrived before the job id was known could not cancel the job itself.
            if (cancelRequested)
            {
                await CancelJobQuietlyAsync(jobId);
                token.ThrowIfCancellationRequested();
            }

            await _api.RunJobAndWaitAsync(jobId, ct: token);

            var outputBytes = await _api.GetJobOutputFileAsync(jobId, "agent_results.json", token)
                ?? throw new InvalidOperationException("agent_results.json missing from job output");

            Complete(layer, Encoding.UTF8.GetString(outputBytes));
        }
        catch (Exception ex) when (token.IsCancellationRequested)
        {
            _logger.LogInformation("Layer {LayerId} cancelled ({Type})", layer.LayerId, ex.GetType().Name);
            Finish(layer, LayerStatus.Cancelled, new ToolError
            {
                Code = ErrorCodes.Cancelled, Message = "The layer was cancelled.", LayerId = layer.LayerId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Layer {LayerId} failed: {Msg}", layer.LayerId, ex.Message);
            Finish(layer, LayerStatus.Failed, Classify(ex) with { LayerId = layer.LayerId });
        }
        finally
        {
            lease?.Dispose();
            layer.Completion.TrySetResult();
        }
    }

    private void Complete(LayerRecord layer, string resultJson)
    {
        var failed = new List<int>();
        string? firstError = null;
        using (var doc = JsonDocument.Parse(resultJson))
        {
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var worker in results.EnumerateArray())
                {
                    if (worker.TryGetProperty("success", out var s) && s.GetBoolean())
                        continue;
                    failed.Add(worker.TryGetProperty("workerIndex", out var i) ? i.GetInt32() : -1);
                    firstError ??= worker.TryGetProperty("errorMessage", out var e) ? e.GetString() : null;
                }
            }
        }

        lock (layer.Sync)
        {
            layer.ResultJson = resultJson;
        }

        if (failed.Count == 0)
        {
            Finish(layer, LayerStatus.Completed, null);
            _logger.LogInformation("Layer {LayerId} completed", layer.LayerId);
            return;
        }

        failed.Sort();
        Finish(layer, LayerStatus.CompletedWithFailures, new ToolError
        {
            Code          = ErrorCodes.WorkerError,
            Message       = $"{failed.Count} of {layer.Parallelism} workers failed. First error: {firstError ?? "unknown"}",
            LayerId       = layer.LayerId,
            FailedWorkers = failed,
        });
        _logger.LogWarning("Layer {LayerId} completed with {Count} failed workers", layer.LayerId, failed.Count);
    }

    private void Finish(LayerRecord layer, LayerStatus status, ToolError? error)
    {
        lock (layer.Sync)
        {
            layer.Status      = status;
            layer.Error       = error;
            layer.CompletedAt = DateTimeOffset.UtcNow;
        }

        Persist(layer);
    }

    /// <summary>Mirrors the layer to the store, unless its session was deleted meanwhile.</summary>
    private void Persist(LayerRecord layer)
    {
        if (_sessions.ContainsKey(layer.SessionId))
            _store.SaveLayer(layer);
    }

    private async Task CancelJobQuietlyAsync(long jobId)
    {
        try
        {
            await _api.CancelJobAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel PARCS job {JobId}", jobId);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Maps a job failure to an error code, from the exception type and the host's message.</summary>
    internal static ToolError Classify(Exception ex)
    {
        var message = ex.Message;

        // The API client wraps HTTP failures, so look through the inner exceptions as well.
        for (var inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner is FlurlHttpException { StatusCode: null or >= 500 } or HttpRequestException)
            {
                return new ToolError
                {
                    Code = ErrorCodes.HostUnavailable, Message = $"PARCS host unreachable: {inner.Message}", Retryable = true,
                };
            }
        }

        if (FailFastPattern().Match(message) is { Success: true } failFast)
        {
            return new ToolError
            {
                Code          = ErrorCodes.WorkerError,
                Message       = message,
                FailedWorkers = [int.Parse(failFast.Groups[1].Value)],
            };
        }

        if (message.Contains("OutOfMemory", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("out of memory", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolError
            {
                Code = ErrorCodes.OutOfMemory,
                Message = message + " Reduce parallelism or the data each worker holds.",
            };
        }

        if (message.Contains("waiting for daemons to connect", StringComparison.OrdinalIgnoreCase))
            return new ToolError { Code = ErrorCodes.DaemonTimeout, Message = message, Retryable = true };

        if (message.Contains("No daemon was resolved", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("No daemons available", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolError { Code = ErrorCodes.NoDaemons, Message = message, Retryable = true };
        }

        return new ToolError { Code = ErrorCodes.JobFailed, Message = message };
    }

    [GeneratedRegex(@"Worker (\d+) failed \(failFast\)")]
    private static partial Regex FailFastPattern();

    private static (string? EntryPoint, string? Error) ResolveEntryPoint(SessionRecord session, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return session.EntryPoints.Count == 1
                ? (session.EntryPoints[0], null)
                : (null, $"The session has several IAgentComputation classes; pass entryPoint, one of: {string.Join(", ", session.EntryPoints)}.");
        }

        var exact = session.EntryPoints.FirstOrDefault(e => e == requested);
        if (exact is not null)
            return (exact, null);

        var bySimpleName = session.EntryPoints
            .Where(e => e.Split('.', '+')[^1] == requested)
            .ToList();
        return bySimpleName.Count == 1
            ? (bySimpleName[0], null)
            : (null, $"entryPoint '{requested}' does not match one class of the session: {string.Join(", ", session.EntryPoints)}.");
    }

    private LayerRecord? FindKeyedLayer(string sessionId, string key)
    {
        lock (_layerKeysLock)
        {
            return _layerKeys.TryGetValue((sessionId, key), out var id) ? GetLayer(id) : null;
        }
    }

    /// <summary>Request options after defaults are applied, as they take part in the fingerprint.</summary>
    private sealed record LayerOptions(int ChannelTimeoutSeconds, string Inherit, bool FailFast, int MaxPoints, int MaxDepth);

    private static string Fingerprint(LayerRequest request, string? entryPoint, LayerOptions options) => Hash(JsonSerializer.Serialize(new
    {
        entryPoint,
        request.Parallelism,
        request.PreviousLayerIds,
        request.CustomData,
        Parameters = request.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal).ToList(),
        request.WorkerInputs,
        options.FailFast,
        options.ChannelTimeoutSeconds,
        options.Inherit,
    })
        // Only spawning layers carry the budget, so layers stored before it keep their fingerprint.
        + (options.MaxPoints > request.Parallelism ? $"|spawn:{options.MaxPoints}:{options.MaxDepth}" : ""));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
