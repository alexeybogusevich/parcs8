using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.AddConsole();

// ── PARCS services ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ParcsApiClient>();
builder.Services.AddSingleton<RoslynCompilerService>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ClusterInfoService>();
builder.Services.AddSingleton<AgentRunnerModuleRegistrar>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentRunnerModuleRegistrar>());

// ── MCP server ────────────────────────────────────────────────────────────────
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<ParcsAgentTools>();

var app = builder.Build();

app.MapMcp();

// Health probe (used by Kubernetes liveness/readiness probes)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
