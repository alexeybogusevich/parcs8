using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Services.Interfaces
{
    internal interface ISignalHandlerFactory
    {
        ISignalHandler Create(Signal signal);
    }
}