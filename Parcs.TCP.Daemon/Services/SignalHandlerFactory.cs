using Parcs.Core;
using Parcs.TCP.Daemon.Handlers;
using Parcs.TCP.Daemon.Handlers.Interfaces;
using Parcs.TCP.Daemon.Services.Interfaces;

namespace Parcs.TCP.Daemon.Services
{
    internal sealed class SignalHandlerFactory : ISignalHandlerFactory
    {
        public ISignalHandler Create(Signal signal)
        {
            return signal switch
            {
                Signal.InitializeJob => new InitializeJobSignalHandler(),
                Signal.ExecuteClass => new ExecuteClassSignalHandler(),
                _ => new DefaultSignalHandler(),
            };
        }
    }
}