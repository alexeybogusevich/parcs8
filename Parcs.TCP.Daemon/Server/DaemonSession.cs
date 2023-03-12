using NetCoreServer;
using System.Net.Sockets;
using System.Net;
using Parcs.Core;
using Parcs.TCP.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Server
{
    internal sealed class DaemonSession : TcpSession, ITransmissonManager
    {
        private readonly ISignalHandlerFactory _signalHandlerFactory;
        private readonly IChannel _channel;

        public DaemonSession(TcpServer server, ISignalHandlerFactory signalHandlerFactory)
            : base(server)
        {
            _signalHandlerFactory = signalHandlerFactory;
            _channel = new Channel(this);
        }

        public override long Send(byte[] buffer)
        {
            Console.WriteLine($"Daemon: Sending {buffer.Length} bytes to the host.");
            return base.Send(buffer);
        }

        protected override void OnConnected()
        {
            var endpoint = Socket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Daemon: Connection to the host ({endpoint?.Address}) was established.");
            Send(new byte[] { (byte)Signal.AcknowledgeConnection });
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Daemon: Connection to the host was lost.");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"Daemon: Received {size} bytes from the host.");

            if (size < 1)
            {
                return;
            }

            var signal = (Signal)buffer[offset];
            var signalHandler = _signalHandlerFactory.Create(signal);

            signalHandler.Handle(_channel);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Daemon session caught an error with code {error}");
        }
    }
}