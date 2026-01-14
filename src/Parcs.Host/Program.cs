using Parcs.Host.Extensions;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHostedService<HostedServices.HostTcpServer>();
builder.Services.AddHttpClient();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

app.MigrateDatabase();

app.UseSwagger();
app.UseSwaggerUI();

app.UseGlobalExceptionHandler();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("health");

app.Run();