namespace Parcs.Modules.AgentRunner.Models;

/// <summary>
/// Serialized to <c>layer_input.json</c> and passed as a job input file.
/// Contains all layer-level parameters; the worker-specific index is injected by the main module.
/// </summary>
public sealed class LayerInputDto
{
    public string SessionId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public int TotalWorkers { get; set; } = 1;
    public string? PreviousLayerResultJson { get; set; }
    public string? CustomData { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}
