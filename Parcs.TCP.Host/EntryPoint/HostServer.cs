using NetCoreServer;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;

namespace Parcs.TCP.Host.EntryPoint
{
    public class HostServer : TcpServer
    {
        public HostServer(IPAddress address, int port) 
            : base(address, port) 
        {
        }

        protected override TcpSession CreateSession() => new HostSession(this);

        protected override void OnError(SocketError error) => Console.WriteLine($"Host Server caught an error with code {error}");

        public ConcurrentDictionary<Guid, TcpSession> ActiveSessions => Sessions;
    }
}