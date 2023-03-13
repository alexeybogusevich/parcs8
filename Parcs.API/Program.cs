using Parcs.Core;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Modules;
using Parcs.HostAPI.Services;
using Parcs.HostAPI.Services.Interfaces;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IMainModule, MainModuleSample>();
builder.Services.AddScoped<IDaemonSelector, DaemonSelector>();
builder.Services.AddScoped<IInputReaderFactory, InputReaderFactory>();
builder.Services.AddScoped<IInputWriter, InputWriter>();
builder.Services.AddScoped<IHostInfoFactory, HostInfoFactory>();
builder.Services.AddSingleton<IJobManager, JobManager>();
builder.Services.AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.Configure<FileSystemConfiguration>(builder.Configuration.GetSection(FileSystemConfiguration.SectionName));
builder.Services.Configure<DefaultDaemonConfiguration>(builder.Configuration.GetSection(DefaultDaemonConfiguration.SectionName));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();