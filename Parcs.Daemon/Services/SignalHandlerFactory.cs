using Parcs.Net;
using Parcs.Daemon.Handlers;
using Parcs.TCP.Daemon.Handlers;
using Parcs.TCP.Daemon.Handlers.Interfaces;
using Parcs.TCP.Daemon.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Parcs.TCP.Daemon.Services
{
    internal sealed class SignalHandlerFactory : ISignalHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SignalHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ISignalHandler Create(Signal signal)
        {
            return signal switch
            {
                Signal.InitializeJob => _serviceProvider.GetRequiredService<InitializeJobSignalHandler>(),
                Signal.ExecuteClass => _serviceProvider.GetRequiredService<ExecuteClassSignalHandler>(),
                _ => _serviceProvider.GetRequiredService<DefaultSignalHandler>(),
            };
        }
    }
}