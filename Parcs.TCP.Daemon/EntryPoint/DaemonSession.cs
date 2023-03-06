using NetCoreServer;
using System.Net.Sockets;
using System.Net;
using Parcs.Core;

namespace Parcs.TCP.Daemon.EntryPoint
{
    internal class DaemonSession : TcpSession, IChannelTransmissonManager
    {
        public DaemonSession(TcpServer server)
            : base(server)
        {
        }

        protected override void OnConnected()
        {
            var endpoint = Socket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Connection to the host ({endpoint?.Address}) was established.");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Connection to the host was lost.");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Received data from the host. Size: {size}.");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Daemon session caught an error with code {error}");
        }
    }
}