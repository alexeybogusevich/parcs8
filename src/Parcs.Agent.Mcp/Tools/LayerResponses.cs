using System.Text.Json;
using System.Text.Json.Nodes;
using Parcs.Agent.Mcp.Models;

namespace Parcs.Agent.Mcp.Tools;

/// <summary>Builds the envelope for a layer in one of the views: summary, outputs, full.</summary>
public static class LayerResponses
{
    public static ToolEnvelope Build(LayerRecord layer, string? view, bool? reused = null)
    {
        lock (layer.Sync)
        {
            var workers = ReadWorkers(layer.ResultJson);
            var full    = string.Equals(view, "full", StringComparison.OrdinalIgnoreCase);
            var outputs = full || string.Equals(view, "outputs", StringComparison.OrdinalIgnoreCase);
            var end     = layer.CompletedAt ?? (layer.StartedAt is null ? null : DateTimeOffset.UtcNow);

            return new ToolEnvelope
            {
                Ok        = layer.Error is null,
                Status    = layer.Status.ToString(),
                SessionId = layer.SessionId,
                LayerId   = layer.LayerId,
                LayerKey  = layer.LayerKey,
                Reused    = reused,
                Layer     = new LayerSummary
                {
                    EntryPoint       = layer.EntryPoint,
                    Parallelism      = layer.Parallelism,
                    PreviousLayerIds = layer.PreviousLayerIds,
                    PeerChannels     = workers?.Sum(w => w.Peers?.Count(p => p > w.WorkerIndex) ?? 0) is > 0 and var channels ? channels : null,
                    MaxPoints        = layer.MaxPoints > layer.Parallelism ? layer.MaxPoints : null,
                    SpawnedPoints    = workers?.Sum(w => w.Spawned) is > 0 and var spawned ? spawned : null,
                    Inherit          = layer.Inherit == Services.InheritModes.ByIndex ? layer.Inherit : null,
                    SuccessCount     = workers?.Count(w => w.Success),
                    FailureCount     = workers?.Count(w => !w.Success),
                    QueuedSeconds    = ((layer.StartedAt ?? layer.CompletedAt ?? DateTimeOffset.UtcNow) - layer.SubmittedAt).TotalSeconds,
                    ElapsedSeconds   = layer.StartedAt is { } start && end is { } e ? (e - start).TotalSeconds : null,
                    SubmittedAt      = layer.SubmittedAt,
                    StartedAt        = layer.StartedAt,
                    CompletedAt      = layer.CompletedAt,
                    JobId            = layer.JobId,
                },
                Output  = layer.Parallelism == 1 && workers is [{ Success: true } only] ? only.Output?.DeepClone() : null,
                Outputs = outputs && workers is not null
                    ? new JsonArray([.. workers.Select(w => w.Success ? w.Output?.DeepClone() : null)])
                    : null,
                Workers = full ? workers : null,
                Hint    = OmittedOutputsHint(layer, workers, outputs),
                Error   = layer.Error,
            };
        }
    }

    // A "Completed" summary without the results reads like the whole answer; say where the results are.
    private static string? OmittedOutputsHint(LayerRecord layer, List<WorkerDetail>? workers, bool outputsShown)
    {
        if (outputsShown || layer.Parallelism == 1 || workers is null)
            return null;

        var withOutput = workers.Count(w => w.Success && w.Output is not null);
        return withOutput == 0 ? null
            : $"{withOutput} worker output{(withOutput == 1 ? " is" : "s are")} not shown in the summary view: " +
              $"call get_layer_result(layerId: \"{layer.LayerId}\", view: \"outputs\"), or pass view: \"outputs\" when running a layer.";
    }

    private static List<WorkerDetail>? ReadWorkers(string? resultJson)
    {
        if (resultJson is null)
            return null;

        using var doc = JsonDocument.Parse(resultJson);
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return [];

        return results.EnumerateArray()
            .Select(r => new WorkerDetail
            {
                WorkerIndex    = r.TryGetProperty("workerIndex", out var i) ? i.GetInt32() : 0,
                Success        = r.TryGetProperty("success", out var s) && s.GetBoolean(),
                Output         = r.TryGetProperty("outputData", out var o) && o.ValueKind == JsonValueKind.String
                                     ? ParseOutput(o.GetString()!) : null,
                Error          = r.TryGetProperty("errorMessage", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null,
                ElapsedSeconds = r.TryGetProperty("elapsedSeconds", out var t) ? t.GetDouble() : 0,
                Spawned        = r.TryGetProperty("spawned", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetInt32() : 0,
                Peers          = r.TryGetProperty("peers", out var p) && p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0
                                     ? [.. p.EnumerateArray().Select(e => e.GetInt32())] : null,
            })
            .OrderBy(w => w.WorkerIndex)
            .ToList();
    }

    // A worker's output is returned parsed when it is JSON, so code does not parse twice.
    private static JsonNode? ParseOutput(string output)
    {
        try
        {
            return JsonNode.Parse(output);
        }
        catch (JsonException)
        {
            return JsonValue.Create(output);
        }
    }
}
