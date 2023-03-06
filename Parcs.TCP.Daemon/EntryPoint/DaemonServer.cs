using NetCoreServer;
using System.Net.Sockets;

namespace Parcs.TCP.Daemon.EntryPoint
{
    internal class DaemonServer : TcpServer
    {
        public DaemonServer(string address, int port)
            : base(address, port)
        {
        }

        protected override TcpSession CreateSession() => new DaemonSession(this);

        protected override void OnError(SocketError error) => Console.WriteLine($"Host Server caught an error with code {error}");
    }
}