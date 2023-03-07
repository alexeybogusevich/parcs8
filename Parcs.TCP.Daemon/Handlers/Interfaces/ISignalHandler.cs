using Parcs.Core;

namespace Parcs.TCP.Daemon.Handlers.Interfaces
{
    internal interface ISignalHandler
    {
        void Handle(byte[] buffer, long offset, long size, IChannel channel);
    }
}