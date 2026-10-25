using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// Ensures the AgentRunner PARCS module (the fixed binary host for user code) is registered
/// in the PARCS host exactly once per MCP server lifecycle.
///
/// The module binary files are expected to be published alongside this assembly under
/// <c>agent-runner-module/</c>.
/// </summary>
public sealed class AgentRunnerModuleRegistrar : IHostedService, IAgentRunnerModule
{
    private const string ModuleSubdir = "agent-runner-module";

    private readonly ParcsApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentRunnerModuleRegistrar> _logger;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>The PARCS module ID after registration — set once at startup.</summary>
    public long ModuleId { get; private set; }

    /// <summary>True once the module has been successfully registered.</summary>
    public bool IsReady => ModuleId > 0;

    /// <summary>The main assembly name expected by the PARCS module loader (no .dll extension — the host validator appends it).</summary>
    public string AssemblyName => "Parcs.Modules.AgentRunner";

    /// <summary>The fully-qualified main-module class name.</summary>
    public string ClassName => "Parcs.Modules.AgentRunner.AgentRunnerMainModule";

    public AgentRunnerModuleRegistrar(
        ParcsApiClient api,
        IConfiguration config,
        ILogger<AgentRunnerModuleRegistrar> logger)
    {
        _api    = api;
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Allow pre-configured module ID (e.g. in production where the module is pre-registered)
        var preconfiguredId = _config.GetValue<long?>("Parcs:AgentRunnerModuleId");
        if (preconfiguredId.HasValue && preconfiguredId.Value > 0)
        {
            ModuleId = preconfiguredId.Value;
            _logger.LogInformation(
                "Using pre-configured AgentRunner module id={Id}", ModuleId);
            return Task.CompletedTask;
        }

        var moduleDir = Path.Combine(
            Path.GetDirectoryName(typeof(AgentRunnerModuleRegistrar).Assembly.Location)!,
            ModuleSubdir);

        if (!Directory.Exists(moduleDir))
        {
            _logger.LogWarning(
                "AgentRunner module directory not found: {Dir}. " +
                "Set Parcs:AgentRunnerModuleId to skip auto-registration.", moduleDir);
            return Task.CompletedTask;
        }

        var dllFiles = Directory.GetFiles(moduleDir, "*.dll");
        if (dllFiles.Length == 0)
        {
            _logger.LogWarning("No DLLs found in {Dir}. Skipping module registration.", moduleDir);
            return Task.CompletedTask;
        }

        var files = dllFiles.Select(path => (
            Filename: Path.GetFileName(path),
            Bytes:    File.ReadAllBytes(path)
        )).ToList();

        // Fire-and-forget: register in the background so the pod becomes Ready immediately.
        // The MCP tools check IsReady before proceeding.
        _ = Task.Run(() => RegisterWithRetryAsync(files, _cts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>First 12 hex digits of a SHA-256 over the files' names and bytes, in name order.</summary>
    internal static string ContentHash(IEnumerable<(string Filename, byte[] Bytes)> files)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var (filename, bytes) in files.OrderBy(f => f.Filename, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(filename));
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset())[..12].ToLowerInvariant();
    }

    private async Task RegisterWithRetryAsync(
        List<(string Filename, byte[] Bytes)> files,
        CancellationToken ct)
    {
        // The name carries a hash of the binaries, so a restart with the same build reuses the
        // module it registered before instead of uploading another copy.
        var moduleName = $"{AssemblyName}-{ContentHash(files)}";

        var delays = new[] { 5, 10, 20, 40, 60, 120 };
        for (int attempt = 0; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                if (await _api.FindModuleAsync(moduleName, ct) is { } existingId)
                {
                    ModuleId = existingId;
                    _logger.LogInformation("AgentRunner module {Name} already registered — id={Id}", moduleName, ModuleId);
                    return;
                }

                ModuleId = await _api.UploadModuleAsync(files, moduleName, ct);
                _logger.LogInformation("AgentRunner module {Name} registered — id={Id}", moduleName, ModuleId);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                var delay = attempt < delays.Length ? delays[attempt] : delays[^1];
                _logger.LogWarning(
                    "Module upload failed (attempt {Attempt}), retrying in {Delay}s: {Message}",
                    attempt + 1, delay, ex.Message);
                try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
