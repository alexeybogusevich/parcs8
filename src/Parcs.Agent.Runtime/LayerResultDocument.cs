using System.Text.Json;

namespace Parcs.Agent.Runtime;

/// <summary>
/// Reads the stored result of a layer: <c>{ sessionId, layerId, totalElapsedSeconds,
/// results: [ { workerIndex, success, outputData, errorMessage, elapsedSeconds } ] }</c>.
/// </summary>
public static class LayerResultDocument
{
    /// <summary>Returns <c>outputData</c> of every worker in worker order; null for failed workers.</summary>
    public static IReadOnlyList<string?> ReadOutputs(string layerResultJson)
    {
        using var doc = JsonDocument.Parse(layerResultJson);
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return [];

        return results.EnumerateArray()
            .OrderBy(r => r.TryGetProperty("workerIndex", out var i) ? i.GetInt32() : 0)
            .Select(r => r.TryGetProperty("success", out var s) && s.GetBoolean()
                      && r.TryGetProperty("outputData", out var o) && o.ValueKind == JsonValueKind.String
                ? o.GetString()
                : null)
            .ToList();
    }
}
