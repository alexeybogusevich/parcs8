using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Handlers;
using Parcs.Daemon.Services;
using Parcs.Daemon.Services.Interfaces;
using Parcs.TCP.Daemon.Handlers;

namespace Parcs.Daemon.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            return services
                .AddSingleton<DefaultSignalHandler>()
                .AddSingleton<ExecuteClassSignalHandler>()
                .AddSingleton<InitializeJobSignalHandler>()
                .AddSingleton<IJobContextAccessor, JobContextAccessor>()
                .AddSingleton<ISignalHandlerFactory, SignalHandlerFactory>();
        }
    }
}