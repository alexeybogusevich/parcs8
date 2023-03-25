using Parcs.Daemon.Handlers.Interfaces;
using Parcs.Net;

namespace Parcs.Daemon.Services.Interfaces
{
    internal interface ISignalHandlerFactory
    {
        ISignalHandler Create(Signal signal);
    }
}