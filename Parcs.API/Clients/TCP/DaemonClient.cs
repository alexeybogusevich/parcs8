using Parcs.Core;
using System.Net;
using System.Net.Sockets;
using TcpClient = NetCoreServer.TcpClient;

namespace Parcs.HostAPI.Clients.TCP
{
    internal sealed class DaemonClient : TcpClient, ITransmissonManager
    {
        private bool _stop;

        public DaemonClient(string address, int port)
            : base(address, port)
        {
        }

        private IPAddress RemoteAddress => (Socket?.RemoteEndPoint as IPEndPoint)?.Address;

        public void DisconnectAndStop()
        {
            _stop = true;
            Disconnect();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Host: Connection to the daemon ({RemoteAddress}) was established.");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Host: Daemon disconnected.");

            Thread.Sleep(1000);

            if (!_stop)
            {
                ConnectAsync();
            }
        }

        public override long Send(byte[] buffer)
        {
            Console.WriteLine($"Host: Sending {buffer.Length} bytes to the daemon.");
            return base.Send(buffer);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Host: Received {size} bytes from the daemon.");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"An error with code {error} occurred during communication with daemon ({RemoteAddress}).");
        }
    }
}