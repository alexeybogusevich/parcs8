using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Mcp.Models;

namespace Parcs.Agent.Mcp.Services;

/// <summary>Durable copy of sessions and layers, so they outlive an MCP server restart.</summary>
public interface ISessionStore
{
    (IReadOnlyList<SessionRecord> Sessions, IReadOnlyList<LayerRecord> Layers) Load();

    void SaveSession(SessionRecord session);

    void SaveLayer(LayerRecord layer);

    void DeleteSession(string sessionId);
}

/// <summary>Keeps nothing: sessions live as long as the process.</summary>
public sealed class NullSessionStore : ISessionStore
{
    public static NullSessionStore Instance { get; } = new();

    public (IReadOnlyList<SessionRecord>, IReadOnlyList<LayerRecord>) Load() => ([], []);

    public void SaveSession(SessionRecord session) { }

    public void SaveLayer(LayerRecord layer) { }

    public void DeleteSession(string sessionId) { }
}

/// <summary>
/// Files under <c>{root}/{sessionId}/</c>: <c>session.json</c>, <c>assembly.dll</c> and
/// <c>layers/{layerId}.json</c>. On Kubernetes the root is on the shared storage volume.
/// A failed write is logged and ignored: persistence never fails a tool call.
/// </summary>
public sealed class FileSessionStore(string root, ILogger<FileSessionStore> logger) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Lock _lock = new();

    public (IReadOnlyList<SessionRecord> Sessions, IReadOnlyList<LayerRecord> Layers) Load()
    {
        var sessions = new List<SessionRecord>();
        var layers = new List<LayerRecord>();
        if (!Directory.Exists(root))
            return (sessions, layers);

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<StoredSession>(File.ReadAllBytes(Path.Combine(directory, "session.json")), JsonOptions)!;
                sessions.Add(new SessionRecord
                {
                    SessionId   = stored.SessionId,
                    CreatedAt   = stored.CreatedAt,
                    SourceCode  = stored.SourceCode,
                    SourceHash  = stored.SourceHash,
                    EntryPoints = stored.EntryPoints,
                    Assembly    = File.ReadAllBytes(Path.Combine(directory, "assembly.dll")),
                });

                var layersDirectory = Path.Combine(directory, "layers");
                if (!Directory.Exists(layersDirectory))
                    continue;

                foreach (var file in Directory.EnumerateFiles(layersDirectory, "*.json"))
                {
                    try
                    {
                        layers.Add(JsonSerializer.Deserialize<StoredLayer>(File.ReadAllBytes(file), JsonOptions)!.ToRecord());
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Skipping unreadable stored layer {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping unreadable stored session {Directory}", directory);
            }
        }

        return (sessions, layers);
    }

    public void SaveSession(SessionRecord session) => Write(() =>
    {
        var directory = Path.Combine(root, session.SessionId);
        Directory.CreateDirectory(directory);
        WriteAtomically(Path.Combine(directory, "assembly.dll"), session.Assembly);
        WriteAtomically(Path.Combine(directory, "session.json"), JsonSerializer.SerializeToUtf8Bytes(new StoredSession
        {
            SessionId   = session.SessionId,
            CreatedAt   = session.CreatedAt,
            SourceCode  = session.SourceCode,
            SourceHash  = session.SourceHash,
            EntryPoints = session.EntryPoints,
        }, JsonOptions));
    }, session.SessionId);

    public void SaveLayer(LayerRecord layer) => Write(() =>
    {
        StoredLayer stored;
        lock (layer.Sync)
        {
            stored = StoredLayer.From(layer);
        }

        var directory = Path.Combine(root, layer.SessionId, "layers");
        Directory.CreateDirectory(directory);
        WriteAtomically(Path.Combine(directory, layer.LayerId + ".json"), JsonSerializer.SerializeToUtf8Bytes(stored, JsonOptions));
    }, layer.LayerId);

    public void DeleteSession(string sessionId) => Write(() =>
    {
        var directory = Path.Combine(root, sessionId);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }, sessionId);

    private void Write(Action write, string id)
    {
        try
        {
            lock (_lock)
            {
                write();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not persist {Id} to {Root}", id, root);
        }
    }

    private static void WriteAtomically(string path, byte[] bytes)
    {
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed class StoredSession
    {
        public string SessionId { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string SourceCode { get; set; } = "";
        public string SourceHash { get; set; } = "";
        public IReadOnlyList<string> EntryPoints { get; set; } = [];
    }

    private sealed class StoredLayer
    {
        public string LayerId { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string? LayerKey { get; set; }
        public string Fingerprint { get; set; } = "";
        public string? EntryPoint { get; set; }
        public int Parallelism { get; set; }
        public IReadOnlyList<string> PreviousLayerIds { get; set; } = [];
        public bool FailFast { get; set; }
        public int ChannelTimeoutSeconds { get; set; }
        public string Inherit { get; set; } = InheritModes.All;
        public int MaxPoints { get; set; }
        public int MaxDepth { get; set; }
        public int Slots { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public LayerStatus Status { get; set; }
        public long? JobId { get; set; }
        public string? ResultJson { get; set; }
        public ToolError? Error { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

        public static StoredLayer From(LayerRecord l) => new()
        {
            LayerId = l.LayerId, SessionId = l.SessionId, LayerKey = l.LayerKey, Fingerprint = l.Fingerprint,
            EntryPoint = l.EntryPoint, Parallelism = l.Parallelism, PreviousLayerIds = l.PreviousLayerIds,
            FailFast = l.FailFast, ChannelTimeoutSeconds = l.ChannelTimeoutSeconds, Inherit = l.Inherit, SubmittedAt = l.SubmittedAt,
            MaxPoints = l.MaxPoints, MaxDepth = l.MaxDepth, Slots = l.Slots,
            Status = l.Status, JobId = l.JobId, ResultJson = l.ResultJson, Error = l.Error,
            StartedAt = l.StartedAt, CompletedAt = l.CompletedAt,
        };

        public LayerRecord ToRecord() => new()
        {
            LayerId = LayerId, SessionId = SessionId, LayerKey = LayerKey, Fingerprint = Fingerprint,
            EntryPoint = EntryPoint, Parallelism = Parallelism, PreviousLayerIds = PreviousLayerIds,
            FailFast = FailFast, ChannelTimeoutSeconds = ChannelTimeoutSeconds, Inherit = Inherit, SubmittedAt = SubmittedAt,
            // Layers stored before spawning existed had no budget: their workers were all their points.
            MaxPoints = MaxPoints > 0 ? MaxPoints : Parallelism, MaxDepth = MaxDepth, Slots = Slots > 0 ? Slots : Parallelism,
            Status = Status, JobId = JobId, ResultJson = ResultJson, Error = Error,
            StartedAt = StartedAt, CompletedAt = CompletedAt,
        };
    }
}
