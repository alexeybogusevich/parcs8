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

            Console.WriteLine($"New daemon connected! SessionId: {Id}, IP address: {endpoint?.Address}, Port: {endpoint?.Port}.");

            // Send invite message
            string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            ReceiveAsync

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
