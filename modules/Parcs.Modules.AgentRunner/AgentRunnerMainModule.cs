using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Modules.AgentRunner.Models;
using Parcs.Net;

namespace Parcs.Modules.AgentRunner;

/// <summary>
/// Main (host-side) module for agent-driven parallel computation.
///
/// Protocol:
///   Input files expected in the job input directory:
///     - agent_computation.dll   — compiled user assembly (implements IAgentComputation)
///     - layer_input.json        — JSON-serialised LayerInputDto
///
///   Module argument:
///     - PointsNumber            — number of daemon workers to spawn (= layer parallelism)
///
///   Steps:
///   1. Read agent assembly bytes and LayerInputDto from input files.
///   2. Batch-create all daemon points (all Service Bus messages published at once so
///      KEDA sees full queue depth immediately).
///   3. Launch worker modules on every point (ExecuteClassAsync BEFORE any data writes).
///   4. Send assembly bytes + worker-specific AgentLayerInput to each worker.
///   5. Collect WorkerResult from every worker (partial failure is tolerated).
///   6. Write agent_results.json (LayerOutputDto) to job output.
/// </summary>
public sealed class AgentRunnerMainModule : IModule
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // --- Step 1: read inputs ---
        var assemblyBytes = ReadInputFile(moduleInfo, "agent_computation.dll");
        var layerInputJson = Encoding.UTF8.GetString(ReadInputFile(moduleInfo, "layer_input.json"));
        var layerInput = JsonSerializer.Deserialize<LayerInputDto>(layerInputJson)
            ?? throw new InvalidOperationException("Failed to deserialise layer_input.json");

        // Prefer TotalWorkers from layer_input.json (set by run_layer parallelism parameter);
        // fall back to PointsNumber from job arguments for backwards compatibility.
        var options = moduleInfo.BindModuleOptions<AgentRunnerOptions>();
        var pointsCount = layerInput.TotalWorkers > 0 ? layerInput.TotalWorkers : options.PointsNumber;

        moduleInfo.Logger.LogInformation(
            "AgentRunner starting: session={Session} layer={Layer} workers={Workers}",
            layerInput.SessionId, layerInput.LayerId, pointsCount);

        // --- Step 2: batch-create all points ---
        var points = await moduleInfo.CreatePointsAsync(pointsCount);
        var channels = new IChannel[pointsCount];
        for (int i = 0; i < pointsCount; i++)
        {
            channels[i] = await points[i].CreateChannelAsync();
        }

        // --- Step 3: launch worker modules (MUST precede any data writes) ---
        foreach (var point in points)
        {
            await point.ExecuteClassAsync<AgentRunnerWorkerModule>();
        }

        moduleInfo.Logger.LogInformation("All {Count} worker modules launched", pointsCount);

        // --- Step 4: send assembly + per-worker input ---
        for (int i = 0; i < pointsCount; i++)
        {
            // Send raw assembly bytes
            await channels[i].WriteDataAsync(assemblyBytes);

            // Send worker-specific input
            var workerInput = new Parcs.Agent.Runtime.AgentLayerInput
            {
                WorkerIndex           = i,
                TotalWorkers          = pointsCount,
                SessionId             = layerInput.SessionId,
                LayerId               = layerInput.LayerId,
                PreviousLayerResultJson = layerInput.PreviousLayerResultJson,
                CustomData            = layerInput.CustomData,
                Parameters            = layerInput.Parameters,
            };
            await channels[i].WriteObjectAsync(workerInput);
        }

        // --- Step 5: collect results (tolerate partial failures) ---
        var results = new List<WorkerResult>(pointsCount);
        for (int i = 0; i < pointsCount; i++)
        {
            try
            {
                var result = await channels[i].ReadObjectAsync<WorkerResult>();
                results.Add(result);
                moduleInfo.Logger.LogInformation(
                    "Worker {Index} finished — success={Success}", i, result.Success);
            }
            catch (Exception ex)
            {
                moduleInfo.Logger.LogWarning(ex, "Worker {Index} failed to return a result: {Msg}", i, ex.Message);
                results.Add(new WorkerResult
                {
                    WorkerIndex  = i,
                    Success      = false,
                    ErrorMessage = $"Communication error: {ex.Message}",
                });
            }
        }

        stopwatch.Stop();

        // --- Step 6: write output ---
        var output = new LayerOutputDto
        {
            SessionId          = layerInput.SessionId,
            LayerId            = layerInput.LayerId,
            Results            = results,
            TotalElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
        };

        var outputJson = JsonSerializer.Serialize(output, JsonOpts);
        await moduleInfo.OutputWriter.WriteToFileAsync(
            Encoding.UTF8.GetBytes(outputJson), "agent_results.json");

        moduleInfo.Logger.LogInformation(
            "AgentRunner completed in {Elapsed:F2}s — {Success}/{Total} workers succeeded",
            stopwatch.Elapsed.TotalSeconds, results.Count(r => r.Success), pointsCount);

        // Cleanup
        foreach (var point in points)
        {
            try { await point.DeleteAsync(); } catch { /* best-effort */ }
        }
        foreach (var channel in channels)
        {
            try { channel.Dispose(); } catch { /* best-effort */ }
        }
    }

    private static byte[] ReadInputFile(IModuleInfo moduleInfo, string filename)
    {
        using var stream = moduleInfo.InputReader.GetFileStreamForFile(filename);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
