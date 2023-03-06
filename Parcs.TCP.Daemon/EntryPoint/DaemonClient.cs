using System.Net.Sockets;
using System.Text;
using TcpClient = NetCoreServer.TcpClient;

namespace Parcs.TCP.Daemon.EntryPoint
{
    internal class DaemonClient : TcpClient
    {
        private bool _stop;

        public DaemonClient(string address, int port)
            : base(address, port)
        {
        }

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
            Console.WriteLine($"Daemon connected. Session Id: {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Daemon disconnected. Session Id: {Id}");

            Thread.Sleep(1000);

            if (!_stop)
            {
                ConnectAsync();
            }
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Daemon caught an error with code {error}");
        }
    }
}