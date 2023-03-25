using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Handlers;
using Parcs.TCP.Daemon.Handlers;
using Parcs.TCP.Daemon.Services;
using Parcs.TCP.Daemon.Services.Interfaces;

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
                .AddSingleton<ISignalHandlerFactory, SignalHandlerFactory>();
        }
    }
}