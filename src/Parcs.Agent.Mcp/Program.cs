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
    .WithHttpTransport(options =>
    {
        // Default is 2 minutes; run_layer blocks synchronously for the whole layer duration
        // (cold starts alone have measured 160-287s), so the default idle window can close the
        // connection before a legitimately long-running layer ever gets to reply.
        options.IdleTimeout = TimeSpan.FromMinutes(30);
    })
    .WithTools<ParcsAgentTools>();

var app = builder.Build();

// ── Auth ──────────────────────────────────────────────────────────────────────
// Minimum viable auth: a shared bearer token checked on every request except the K8s
// health/callback endpoints. Set via the Authentication__BearerToken env var (see
// kube/deployment.gcp.yaml — sourced from a Secret, never committed).
var bearerToken = builder.Configuration["Authentication:BearerToken"];
if (!string.IsNullOrEmpty(bearerToken))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/health" || context.Request.Path == "/noop")
        {
            await next();
            return;
        }

        var expected = $"Bearer {bearerToken}";
        if (context.Request.Headers.Authorization.ToString() != expected)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next();
    });
}
else
{
    app.Logger.LogWarning(
        "Authentication:BearerToken is not set — the MCP endpoint is running with NO authentication.");
}

app.MapMcp();

// Health probe (used by Kubernetes liveness/readiness probes)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Callback sink — PARCS host POSTs here when async jobs complete; we ignore it (use SSE instead).
app.MapPost("/noop", () => Results.Ok());

app.Run();
