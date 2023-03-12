using NetCoreServer;
using Parcs.TCP.Daemon.Services.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Daemon.Server
{
    internal sealed class DaemonServer : TcpServer
    {
        private readonly ISignalHandlerFactory _signalHandlerFactory;

        public DaemonServer(IPAddress address, int port, ISignalHandlerFactory signalHandlerFactory)
            : base(address, port)
        {
            _signalHandlerFactory = signalHandlerFactory;
        }

        protected override TcpSession CreateSession() => new DaemonSession(this, _signalHandlerFactory);

        protected override void OnError(SocketError error) => Console.WriteLine($"Host Server caught an error with code {error}");
    }
}