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
public sealed class AgentRunnerModuleRegistrar : IHostedService
{
    private const string ModuleSubdir = "agent-runner-module";

    private readonly ParcsApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentRunnerModuleRegistrar> _logger;

    /// <summary>The PARCS module ID after registration — set once at startup.</summary>
    public long ModuleId { get; private set; }

    /// <summary>The main assembly name expected by the PARCS module loader.</summary>
    public string AssemblyName => "Parcs.Modules.AgentRunner.dll";

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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Allow pre-configured module ID (e.g. in production where the module is pre-registered)
        var preconfiguredId = _config.GetValue<long?>("Parcs:AgentRunnerModuleId");
        if (preconfiguredId.HasValue && preconfiguredId.Value > 0)
        {
            ModuleId = preconfiguredId.Value;
            _logger.LogInformation(
                "Using pre-configured AgentRunner module id={Id}", ModuleId);
            return;
        }

        var moduleDir = Path.Combine(
            Path.GetDirectoryName(typeof(AgentRunnerModuleRegistrar).Assembly.Location)!,
            ModuleSubdir);

        if (!Directory.Exists(moduleDir))
        {
            _logger.LogWarning(
                "AgentRunner module directory not found: {Dir}. " +
                "Set Parcs:AgentRunnerModuleId to skip auto-registration.", moduleDir);
            return;
        }

        var dllFiles = Directory.GetFiles(moduleDir, "*.dll");
        if (dllFiles.Length == 0)
        {
            _logger.LogWarning("No DLLs found in {Dir}. Skipping module registration.", moduleDir);
            return;
        }

        var files = dllFiles.Select(path => (
            Filename: Path.GetFileName(path),
            Bytes:    File.ReadAllBytes(path)
        )).ToList();

        ModuleId = await _api.UploadModuleAsync(
            files, "Parcs.Modules.AgentRunner", cancellationToken);

        _logger.LogInformation("AgentRunner module registered — id={Id}", ModuleId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
