using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class InitializeJobSignalHandler : ISignalHandler
    {
        public void Handle(byte[] buffer, long offset, long size, IChannel channel)
        {
            return;
        }
    }
}