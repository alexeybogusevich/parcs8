using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.AddConsole();

// ── PARCS services ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ParcsApiClient>();
builder.Services.AddSingleton<IParcsApiClient>(sp => sp.GetRequiredService<ParcsApiClient>());
builder.Services.AddSingleton<RoslynCompilerService>();
builder.Services.AddSingleton<CapacityGate>();
// Sessions and finished layers survive restarts when Parcs:SessionStorePath is set (shared storage on Kubernetes).
builder.Services.AddSingleton<ISessionStore>(sp => builder.Configuration["Parcs:SessionStorePath"] is { Length: > 0 } path
    ? new FileSessionStore(path, sp.GetRequiredService<ILogger<FileSessionStore>>())
    : NullSessionStore.Instance);
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ClusterInfoService>();
builder.Services.AddSingleton<IClusterInfoService>(sp => sp.GetRequiredService<ClusterInfoService>());
builder.Services.AddSingleton<AgentRunnerModuleRegistrar>();
builder.Services.AddSingleton<IAgentRunnerModule>(sp => sp.GetRequiredService<AgentRunnerModuleRegistrar>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentRunnerModuleRegistrar>());

// ── MCP server ────────────────────────────────────────────────────────────────
// Streamable HTTP, stateless: all cross-call state travels as server-minted handles
// (sessionId, layerId, layerKey) in tool arguments, and no call blocks longer than its
// waitSeconds (≤ 200 s), so neither protocol sessions nor long idle windows are needed.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<ParcsAgentTools>();

var app = builder.Build();

app.MapMcp();

// Health probe (used by Kubernetes liveness/readiness probes)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Callback sink — PARCS host POSTs here when async jobs complete; we ignore it (use SSE instead).
app.MapPost("/noop", () => Results.Ok());

app.Run();
