using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Handlers;
using Parcs.Daemon.Services;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Shared.Services.Interfaces;
using Parcs.Shared.Services;
using Parcs.TCP.Daemon.Handlers;
using Microsoft.Extensions.Configuration;
using Parcs.Daemon.Configuration;

namespace Parcs.Daemon.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services
                .AddSingleton<DefaultSignalHandler>()
                .AddSingleton<ExecuteClassSignalHandler>()
                .AddSingleton<InitializeJobSignalHandler>()
                .AddSingleton(typeof(ITypeLoader<>), typeof(TypeLoader<>))
                .AddSingleton<IJobContextAccessor, JobContextAccessor>()
                .AddSingleton<ISignalHandlerFactory, SignalHandlerFactory>();
        }

        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .Configure<NodeConfiguration>(configuration.GetSection(NodeConfiguration.SectionName));
        }
    }
}