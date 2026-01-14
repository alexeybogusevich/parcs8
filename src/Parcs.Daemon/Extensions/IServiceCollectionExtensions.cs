using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Handlers;
using Parcs.Daemon.Services;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Services;
using Microsoft.Extensions.Configuration;
using Parcs.Daemon.Configuration;
using Parcs.Core.Configuration;
using System.Threading.Channels;
using Parcs.Core.Models;
using Microsoft.Extensions.Logging;

namespace Parcs.Daemon.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddSingleton(typeof(ITypeLoader<>), typeof(TypeLoader<>))
                .AddSingleton<CancelJobSignalHandler>()
                .AddSingleton<ConfigurationDaemonResolutionStrategy>()
                .AddSingleton<DefaultSignalHandler>()
                .AddSingleton<ExecuteClassSignalHandler>()
                .AddSingleton<IAddressResolver, AddressResolver>()
                .AddSingleton<IAssemblyPathBuilder, AssemblyPathBuilder>()
                .AddSingleton<IArgumentsProviderFactory, ArgumentsProviderFactory>()
                .AddSingleton<IChannelOrchestrator, ChannelOrchestrator>()
                .AddSingleton<IDaemonResolutionStrategyFactory, DaemonResolutionStrategyFactory>()
                .AddSingleton<IDaemonResolver, DaemonResolver>()
                .AddSingleton<IInputOutputFactory, InputOutputFactory>()
                .AddSingleton<IInternalChannelManager, InternalChannelManager>()
                .AddSingleton<IIsolatedLoadContextProvider, IsolatedLoadContextProvider>()
                .AddSingleton<IJobContextAccessor, JobContextAccessor>()
                .AddSingleton<IJobDirectoryPathBuilder, JobDirectoryPathBuilder>()
                .AddSingleton<IModuleInfoFactory, ModuleInfoFactory>()
                .AddSingleton<IModuleDirectoryPathBuilder, ModuleDirectoryPathBuilder>()
                .AddSingleton<IModuleInfoFactory, ModuleInfoFactory>()
                .AddSingleton<IModuleLoader, ModuleLoader>()
                .AddSingleton<InitializeJobSignalHandler>()
                .AddSingleton<ISignalHandlerFactory, SignalHandlerFactory>()
                .AddSingleton<KubernetesDaemonResolutionStrategy>()
                .AddSingleton(Channel.CreateUnbounded<InternalChannelReference>(new UnboundedChannelOptions() { SingleReader = true }))
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Reader)
                .AddSingleton(svc => svc.GetRequiredService<Channel<InternalChannelReference>>().Writer);
        }

        public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            var hostApiConfiguration = configuration
                .GetSection(HostConfiguration.SectionName)
                .Get<HostConfiguration>();

            services.AddHttpClient<IHostApiClient, HostApiClient>(client =>
            {
                client.BaseAddress = new Uri($"http://{hostApiConfiguration.Uri}:80");
            });

            return services;
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<ApplicationInsightsConfiguration>(configuration.GetSection(ApplicationInsightsConfiguration.SectionName))
                .Configure<DaemonsConfiguration>(configuration.GetSection(DaemonsConfiguration.SectionName))
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<HostingConfiguration>(configuration.GetSection(HostingConfiguration.SectionName))
                .Configure<KubernetesConfiguration>(configuration.GetSection(KubernetesConfiguration.SectionName))
                .Configure<DaemonConfiguration>(configuration.GetSection(DaemonConfiguration.SectionName))
                .Configure<HostConfiguration>(configuration.GetSection(HostConfiguration.SectionName))
                .Configure<ServiceBusConfiguration>(configuration.GetSection(ServiceBusConfiguration.SectionName));
        }

        public static IServiceCollection AddApplicationLogging(this IServiceCollection services, IConfiguration configuration)
        {
            var applicationInsightsConfiguration = configuration
                .GetSection(ApplicationInsightsConfiguration.SectionName)
                .Get<ApplicationInsightsConfiguration>();

            if (applicationInsightsConfiguration?.ConnectionString is null)
            {
                return services;
            }

            services.AddLogging(builder => builder.AddApplicationInsights(
                configureTelemetryConfiguration: (config) => config.ConnectionString = applicationInsightsConfiguration.ConnectionString,
                configureApplicationInsightsLoggerOptions: (options) => { }));

            return services;
        }
    }
}