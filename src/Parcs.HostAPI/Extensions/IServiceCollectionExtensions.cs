using FluentValidation;
using FluentValidation.AspNetCore;
using Parcs.HostAPI.Background;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Pipeline.Validators;
using Parcs.HostAPI.Services;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.Shared.Services;
using Parcs.Shared.Services.Interfaces;
using System.Reflection;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

namespace Parcs.HostAPI.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddAsynchronousJobProcessing(this IServiceCollection services)
        {
            return services
                .AddSingleton(Channel.CreateUnbounded<RunJobAsynchronouslyCommand>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<RunJobAsynchronouslyCommand>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<RunJobAsynchronouslyCommand>>().Writer)
                .AddHostedService<AsynchronousJobRunner>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<JobsConfiguration>(configuration.GetSection(JobsConfiguration.SectionName))
                .Configure<JobOutputConfiguration>(configuration.GetSection(JobOutputConfiguration.SectionName))
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<DefaultDaemonConfiguration>(configuration.GetSection(DefaultDaemonConfiguration.SectionName));
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IGuidReference, GuidReference>()
                .AddScoped<IDaemonSelector, DaemonSelector>()
                .AddScoped<IHostInfoFactory, HostInfoFactory>()
                .AddScoped<IInputOutputFactory, InputOutputFactory>()
                .AddScoped<IJobCompletionNotifier, JobCompletionNotifier>()
                .AddScoped(typeof(ITypeLoader<>), typeof(TypeLoader<>))
                .AddScoped<IMainModuleLoader, MainModuleLoader>()
                .AddSingleton<IJobDirectoryPathBuilder, JobDirectoryPathBuilder>()
                .AddSingleton<IModuleDirectoryPathBuilder, ModuleDirectoryPathBuilder>()
                .AddSingleton<IFileArchiver, FileArchiver>()
                .AddSingleton<IFileSaver, FileSaver>()
                .AddSingleton<IFileReader, FileReader>()
                .AddSingleton<IFileEraser, FileEraser>()
                .AddSingleton<IJobManager, JobManager>()
                .AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        }

        public static IServiceCollection AddValidation(this IServiceCollection services)
        {
            return services
                .AddValidatorsFromAssemblyContaining<GetJobQueryValidator>()
                .AddFluentValidationAutoValidation();
        }
    }
}