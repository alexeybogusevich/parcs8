using NetCoreServer;
using System.Net.Sockets;
using System.Net;
using Parcs.Core;
using Parcs.TCP.Daemon.Services.Interfaces;

namespace Parcs.TCP.Daemon.EntryPoint
{
    internal class DaemonSession : TcpSession, ITransmissonManager
    {
        private readonly ISignalHandlerFactory _signalHandlerFactory;
        private readonly IChannel _channel;

        public DaemonSession(TcpServer server, ISignalHandlerFactory signalHandlerFactory)
            : base(server)
        {
            _signalHandlerFactory = signalHandlerFactory;
            _channel = new Channel(this);
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
            if (size < 1)
            {
                return;
            }

            var signal = (Signal)buffer[offset];
            var signalHandler = _signalHandlerFactory.Create(signal);

            var offsetAfterSignal = offset + 1;
            var sizeAfterSignal = size - 1;

            signalHandler.Handle(buffer, offsetAfterSignal, sizeAfterSignal, _channel);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Daemon session caught an error with code {error}");
        }
    }
}