﻿using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Handlers;
using Parcs.Daemon.Services;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Core.Services.Interfaces;
using Parcs.Core.Services;
using Parcs.TCP.Daemon.Handlers;
using Microsoft.Extensions.Configuration;
using Parcs.Daemon.Configuration;
using Parcs.Core.Configuration;

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
                .AddSingleton<IArgumentsProviderFactory, ArgumentsProviderFactory>()
                .AddSingleton<IDaemonResolutionStrategyFactory, DaemonResolutionStrategyFactory>()
                .AddSingleton<IDaemonResolver, DaemonResolver>()
                .AddSingleton<IInputOutputFactory, InputOutputFactory>()
                .AddSingleton<IJobContextAccessor, JobContextAccessor>()
                .AddSingleton<IJobDirectoryPathBuilder, JobDirectoryPathBuilder>()
                .AddSingleton<IModuleInfoFactory, ModuleInfoFactory>()
                .AddSingleton<IModuleDirectoryPathBuilder, ModuleDirectoryPathBuilder>()
                .AddSingleton<IModuleInfoFactory, ModuleInfoFactory>()
                .AddSingleton<IModuleLoader, ModuleLoader>()
                .AddSingleton<InitializeJobSignalHandler>()
                .AddSingleton<ISignalHandlerFactory, SignalHandlerFactory>()
                .AddSingleton<KubernetesDaemonResolutionStrategy>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<DaemonsConfiguration>(configuration.GetSection(DaemonsConfiguration.SectionName))
                .Configure<FileSystemConfiguration>(configuration.GetSection(FileSystemConfiguration.SectionName))
                .Configure<HostingConfiguration>(configuration.GetSection(HostingConfiguration.SectionName))
                .Configure<KubernetesConfiguration>(configuration.GetSection(KubernetesConfiguration.SectionName))
                .Configure<NodeConfiguration>(configuration.GetSection(NodeConfiguration.SectionName));
        }
    }
}