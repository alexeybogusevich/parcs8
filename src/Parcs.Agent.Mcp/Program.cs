using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.AddConsole();

// ── PARCS services ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
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

// Callback sink — PARCS host POSTs here when async jobs complete; we ignore it (use SSE instead).
app.MapPost("/noop", () => Results.Ok());

app.Run();
