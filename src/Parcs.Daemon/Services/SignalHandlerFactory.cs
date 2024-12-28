using Parcs.Net;
using Parcs.Daemon.Handlers;
using Parcs.TCP.Daemon.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Parcs.Daemon.Services.Interfaces;
using Parcs.Daemon.Handlers.Interfaces;

namespace Parcs.Daemon.Services
{
    internal sealed class SignalHandlerFactory(IServiceProvider serviceProvider) : ISignalHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public ISignalHandler Create(Signal signal)
        {
            return signal switch
            {
                Signal.CancelJob => _serviceProvider.GetRequiredService<CancelJobSignalHandler>(),
                Signal.InitializeJob => _serviceProvider.GetRequiredService<InitializeJobSignalHandler>(),
                Signal.ExecuteClass => _serviceProvider.GetRequiredService<ExecuteClassSignalHandler>(),
                _ => _serviceProvider.GetRequiredService<DefaultSignalHandler>(),
            };
        }
    }
}