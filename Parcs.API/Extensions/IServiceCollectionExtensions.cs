using Parcs.Core;
using Parcs.HostAPI.Background;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.HostAPI.Services;
using System.Reflection;
using Parcs.Modules.Sample;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

namespace Parcs.HostAPI.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddJobScheduling(this IServiceCollection services)
        {
            return services
                .AddSingleton(Channel.CreateUnbounded<ScheduleJobRunCommand>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<ScheduleJobRunCommand>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<ScheduleJobRunCommand>>().Writer)
                .AddHostedService<ScheduledJobRunProcessor>();
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
                .AddScoped<IInputSaver, InputSaver>()
                .AddScoped<IMainModule, SampleMainModule>()
                .AddScoped<IJobCompletionNotifier, JobCompletionNotifier>()
                .AddSingleton<IJobManager, JobManager>()
                .AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        }
    }
}