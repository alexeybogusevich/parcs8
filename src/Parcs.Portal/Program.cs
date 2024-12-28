using Microsoft.AspNetCore.ResponseCompression;
using Parcs.Portal.Extensions;
using Parcs.Portal.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddApplicationServices();
builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddHttpClients(builder.Configuration);
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
});

var app = builder.Build();

app.UseResponseCompression();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();
app.MapControllers();

app.MapBlazorHub();
app.MapHub<JobCompletionHub>("/jobCompletionHub");
app.MapFallbackToPage("/_Host");

app.Run();