using Parcs.Host.Extensions;
using Parcs.Host.HostedServices;

var builder = WebApplication.CreateBuilder(args);

// SynchronousJobRuns blocks until all KEDA daemon pods complete — extend timeouts.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout    = TimeSpan.FromMinutes(15);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

builder.Host.AddElasticSearchLogging(builder.Configuration);
builder.Services.AddApiControllers();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddApplicationServices();
builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddAsynchronousJobProcessing();
builder.Services.AddSingleton<HostTcpServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HostTcpServer>());
builder.Services.AddHttpClient();
builder.Services.AddDatabase(builder.Configuration);
var app = builder.Build();

app.MigrateDatabase();

app.UseSwagger();
app.UseSwaggerUI();

app.UseGlobalExceptionHandler();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("health");

app.Run();