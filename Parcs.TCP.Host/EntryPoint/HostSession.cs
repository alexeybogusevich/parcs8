using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Parcs.TCP.Host.EntryPoint
{
    internal class HostSession : TcpSession
    {
        public HostSession(TcpServer server) 
            : base(server)
        {
        }

        protected override void OnConnected()
        {
            var endpoint = Socket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Connection to daemon ({endpoint?.Address}) is established.");
        }

        protected override void OnDisconnected()
        {
            var endpoint = Socket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Connection to daemon ({endpoint?.Address}) is lost.");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Host Session caught an error with code {error}");
        }
    }
}
