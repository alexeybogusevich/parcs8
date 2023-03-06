using Parcs.Core;
using System.Net;
using System.Net.Sockets;
using TcpClient = NetCoreServer.TcpClient;

namespace Parcs.TCP.Host.EntryPoint
{
    internal class DaemonClient : TcpClient, IChannelTransmissonManager
    {
        private bool _stop;

        public DaemonClient(string address, int port)
            : base(address, port)
        {
        }

        protected IPAddress RemoteAddress => (Socket.RemoteEndPoint as IPEndPoint)?.Address;

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();

            while (IsConnected)
            {
                Thread.Yield();
            }
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Connected to a daemon ({RemoteAddress}).");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Daemon disconnected.");

            Thread.Sleep(1000);

            if (!_stop)
            {
                ConnectAsync();
            }
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Received data from a daemon ({RemoteAddress}). Size: {size}.");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Daemon ({RemoteAddress}) caught an error with code {error}.");
        }
    }
}