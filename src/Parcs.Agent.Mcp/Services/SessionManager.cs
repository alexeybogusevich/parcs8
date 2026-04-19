using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Mcp.Models;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// In-memory store for sessions and layers.
/// Sessions map compiled user code to a PARCS module+assembly;
/// Layers map individual parallel executions to PARCS jobs.
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    private readonly ConcurrentDictionary<string, LayerRecord>   _layers   = new();

    private readonly RoslynCompilerService         _compiler;
    private readonly ParcsApiClient                _api;
    private readonly AgentRunnerModuleRegistrar    _moduleRegistrar;
    private readonly ILogger<SessionManager>       _logger;

    public SessionManager(
        RoslynCompilerService      compiler,
        ParcsApiClient             api,
        AgentRunnerModuleRegistrar moduleRegistrar,
        ILogger<SessionManager>    logger)
    {
        _compiler        = compiler;
        _api             = api;
        _moduleRegistrar = moduleRegistrar;
        _logger          = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // Session management
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <paramref name="sourceCode"/>, uploads the resulting DLL as a job-level
    /// input file, and creates a <see cref="SessionRecord"/>.
    /// The compiled DLL is stored per-session; it is re-sent to PARCS with every layer job.
    /// </summary>
    public SessionRecord CreateSession(string sourceCode)
    {
        var assemblyBytes = _compiler.Compile(sourceCode);

        var session = new SessionRecord
        {
            SourceCode  = sourceCode,
            // We store the compiled bytes so the MCP server can attach them to each job.
            // ParcsModuleId is fixed — it points to the AgentRunner module binary, not user code.
            ParcsModuleId = _moduleRegistrar.ModuleId,
        };

        // Attach compiled bytes to the session record using a side field (boxed in record).
        SessionCompiledAssemblies[session.SessionId] = assemblyBytes;

        _sessions[session.SessionId] = session;

        _logger.LogInformation(
            "Session {Id} created — assembly size={Size} bytes",
            session.SessionId, assemblyBytes.Length);

        return session;
    }

    public SessionRecord? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    /// <summary>Returns all sessions ordered by creation time descending.</summary>
    public IReadOnlyList<SessionRecord> ListSessions() =>
        [.. _sessions.Values.OrderByDescending(s => s.CreatedAt)];

    public byte[]? GetCompiledAssembly(string sessionId) =>
        SessionCompiledAssemblies.TryGetValue(sessionId, out var b) ? b : null;

    // Internal store for compiled assembly bytes (keyed by sessionId)
    internal ConcurrentDictionary<string, byte[]> SessionCompiledAssemblies = new();

    // ─────────────────────────────────────────────────────────────────
    // Layer management
    // ─────────────────────────────────────────────────────────────────

    public LayerRecord CreateLayer(string sessionId)
    {
        var layer = new LayerRecord { SessionId = sessionId };
        _layers[layer.LayerId] = layer;
        return layer;
    }

    public LayerRecord? GetLayer(string layerId) =>
        _layers.TryGetValue(layerId, out var l) ? l : null;

    public void UpdateLayer(string layerId, Action<LayerRecord> updater)
    {
        if (_layers.TryGetValue(layerId, out var l))
            updater(l);
    }

    // ─────────────────────────────────────────────────────────────────
    // Layer execution (background task)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a layer synchronously: submits it, waits for completion, and returns the layer record.
    /// The layer's status is set to Completed or Failed before this method returns.
    /// Use this when the caller wants to block until results are available (e.g. the run_layer MCP tool).
    /// </summary>
    public async Task<LayerRecord> RunLayerSyncAsync(
        string sessionId,
        int parallelism,
        string? previousLayerResultJson,
        string? customData,
        Dictionary<string, string> parameters,
        byte[]? datasetBytes,
        CancellationToken ct)
    {
        var session = GetSession(sessionId)
            ?? throw new InvalidOperationException($"Session '{sessionId}' not found.");

        var layer = CreateLayer(sessionId);
        UpdateLayer(layer.LayerId, l => l.Status = LayerStatus.Running);

        try
        {
            var resultJson = await RunLayerAsync(
                layer, session, parallelism,
                previousLayerResultJson, customData, parameters, datasetBytes, ct);

            UpdateLayer(layer.LayerId, l =>
            {
                l.Status     = LayerStatus.Completed;
                l.ResultJson = resultJson;
                l.CompletedAt = DateTimeOffset.UtcNow;
            });

            _logger.LogInformation("Layer {LayerId} completed (sync)", layer.LayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Layer {LayerId} failed (sync): {Msg}", layer.LayerId, ex.Message);
            UpdateLayer(layer.LayerId, l =>
            {
                l.Status       = LayerStatus.Failed;
                l.ErrorMessage = ex.Message;
                l.CompletedAt  = DateTimeOffset.UtcNow;
            });
        }

        return GetLayer(layer.LayerId)!;
    }

    /// <summary>
    /// Starts executing a layer in a background task.
    /// Callers can poll via <see cref="GetLayer"/> to check status.
    /// </summary>
    public void SubmitLayerBackground(
        LayerRecord layer,
        SessionRecord session,
        int parallelism,
        string? previousLayerResultJson,
        string? customData,
        Dictionary<string, string> parameters,
        byte[]? datasetBytes,
        CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            UpdateLayer(layer.LayerId, l => l.Status = LayerStatus.Running);
            try
            {
                var resultJson = await RunLayerAsync(
                    layer, session, parallelism,
                    previousLayerResultJson, customData, parameters, datasetBytes, ct);

                UpdateLayer(layer.LayerId, l =>
                {
                    l.Status      = LayerStatus.Completed;
                    l.ResultJson  = resultJson;
                    l.CompletedAt = DateTimeOffset.UtcNow;
                });

                _logger.LogInformation("Layer {LayerId} completed", layer.LayerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Layer {LayerId} failed: {Msg}", layer.LayerId, ex.Message);
                UpdateLayer(layer.LayerId, l =>
                {
                    l.Status       = LayerStatus.Failed;
                    l.ErrorMessage = ex.Message;
                    l.CompletedAt  = DateTimeOffset.UtcNow;
                });
            }
        }, ct);
    }

    private async Task<string> RunLayerAsync(
        LayerRecord   layer,
        SessionRecord session,
        int           parallelism,
        string?       previousLayerResultJson,
        string?       customData,
        Dictionary<string, string> parameters,
        byte[]?       datasetBytes,
        CancellationToken ct)
    {
        var assemblyBytes = GetCompiledAssembly(session.SessionId)
            ?? throw new InvalidOperationException($"No compiled assembly for session {session.SessionId}");

        if (!_moduleRegistrar.IsReady)
            throw new InvalidOperationException(
                "AgentRunner module is not yet registered with the PARCS host. Retry in a few seconds.");

        // Build layer_input.json — use PascalCase to match LayerInputDto property names
        // (System.Text.Json deserialisation is case-sensitive by default).
        var layerInputDto = new
        {
            SessionId               = session.SessionId,
            LayerId                 = layer.LayerId,
            TotalWorkers            = parallelism,
            PreviousLayerResultJson = previousLayerResultJson,
            CustomData              = customData,
            Parameters              = parameters,
            HasDataset              = datasetBytes is not null,
        };
        var layerInputBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(layerInputDto));

        // Build input file list — dataset.bin is optional.
        // Workers read it with File.ReadAllBytes("dataset.bin") and check HasDataset first.
        List<(string, byte[])> inputFiles =
        [
            ("agent_computation.dll", assemblyBytes),
            ("layer_input.json",      layerInputBytes),
        ];
        if (datasetBytes is not null)
            inputFiles.Add(("dataset.bin", datasetBytes));

        // Create PARCS job
        var jobId = await _api.CreateJobAsync(
            moduleId:     _moduleRegistrar.ModuleId,
            assemblyName: _moduleRegistrar.AssemblyName,
            className:    _moduleRegistrar.ClassName,
            inputFiles:   inputFiles,
            arguments: new Dictionary<string, string>
            {
                ["PointsNumber"] = parallelism.ToString(),
            },
            ct: ct);

        // Submit async and stream SSE events until completion.
        // This replaces the old blocking SynchronousJobRuns call which caused
        // proxy timeouts when jobs ran silently for more than a minute.
        await _api.RunJobAndWaitAsync(jobId, ct: ct);

        // Fetch output
        var outputBytes = await _api.GetJobOutputFileAsync(jobId, "agent_results.json", ct)
            ?? throw new InvalidOperationException("agent_results.json missing from job output");

        return Encoding.UTF8.GetString(outputBytes);
    }
}
