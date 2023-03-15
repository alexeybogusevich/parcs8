using Parcs.Core;
using Parcs.Daemon.Handlers;
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
                Signal.ExecuteClass => new ExecuteClassSignalHandler(),
                _ => new DefaultSignalHandler(),
            };
        }
    }
}