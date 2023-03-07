using Parcs.Core;

namespace Parcs.TCP.Daemon.Handlers.Interfaces
{
    internal interface ISignalHandler
    {
        void Handle(IChannel channel);
    }
}