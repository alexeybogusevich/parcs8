using FluentValidation;
using FluentValidation.AspNetCore;
using Parcs.HostAPI.Configuration;
using Parcs.HostAPI.Models.Commands;
using Parcs.HostAPI.Services;
using Parcs.HostAPI.Services.Interfaces;
using Parcs.HostAPI.Validators;
using Parcs.Core.Configuration;
using Parcs.Core.Services;
using Parcs.Core.Services.Interfaces;
using System.Reflection;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
using Parcs.HostAPI.HostedServices;
using Parcs.Core.Models;

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
                .Configure<HostingConfiguration>(configuration.GetSection(HostingConfiguration.SectionName))
                .Configure<JobsConfiguration>(configuration.GetSection(JobsConfiguration.SectionName))
                .Configure<JobOutputConfiguration>(configuration.GetSection(JobOutputConfiguration.SectionName))
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<DaemonsConfiguration>(configuration.GetSection(DaemonsConfiguration.SectionName))
                .Configure<KubernetesConfiguration>(configuration.GetSection(KubernetesConfiguration.SectionName));
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IAddressResolver, AddressResolver>()
                .AddScoped<IGuidReference, GuidReference>()
                .AddScoped<IDaemonResolver, DaemonResolver>()
                .AddScoped<IDaemonResolutionStrategyFactory, DaemonResolutionStrategyFactory>()
                .AddScoped<ConfigurationDaemonResolutionStrategy>()
                .AddScoped<KubernetesDaemonResolutionStrategy>()
                .AddScoped<IModuleInfoFactory, ModuleInfoFactory>()
                .AddScoped<IInputOutputFactory, InputOutputFactory>()
                .AddScoped<IJobCompletionNotifier, JobCompletionNotifier>()
                .AddScoped(typeof(ITypeLoader<>), typeof(TypeLoader<>))
                .AddScoped<IModuleLoader, ModuleLoader>()
                .AddScoped<IArgumentsProviderFactory, ArgumentsProviderFactory>()
                .AddSingleton<IJobDirectoryPathBuilder, JobDirectoryPathBuilder>()
                .AddSingleton<IModuleDirectoryPathBuilder, ModuleDirectoryPathBuilder>()
                .AddSingleton<IFileArchiver, FileArchiver>()
                .AddSingleton<IFileSaver, FileSaver>()
                .AddSingleton<IFileReader, FileReader>()
                .AddSingleton<IFileEraser, FileEraser>()
                .AddSingleton<IJobManager, JobManager>()
                .AddSingleton<IInternalChannelManager, InternalChannelManager>()
                .AddSingleton<IIsolatedLoadContextProvider, IsolatedLoadContextProvider>()
                .AddSingleton(Channel.CreateUnbounded<InternalChannelReference>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Writer)
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