using Parcs.Core;
using Parcs.HostAPI.Background;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Modules;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.HostAPI.Services;
using System.Reflection;

namespace Parcs.HostAPI.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddJobScheduling(this IServiceCollection services)
        {
            return services
                .AddSingleton(System.Threading.Channels.Channel.CreateUnbounded<ScheduleJobCommand>())
                .AddHostedService<ScheduledJobProcessor>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<DefaultDaemonConfiguration>(configuration.GetSection(DefaultDaemonConfiguration.SectionName));
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IDaemonSelector, DaemonSelector>()
                .AddScoped<IHostInfoFactory, HostInfoFactory>()
                .AddScoped<IInputReaderFactory, InputReaderFactory>()
                .AddScoped<IInputWriter, InputWriter>()
                .AddScoped<IJobCompletionNotifier, JobCompletionNotifier>()
                .AddScoped<IMainModule, MainModuleSample>()
                .AddSingleton<IJobManager, JobManager>()
                .AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        }
    }
}