using Parcs.Net;
using Parcs.Shared.Models;
using System.Net.Sockets;

namespace Parcs.TCP.Host.Models
{
    internal sealed class Point : IPoint, IDisposable
    {
        private TcpClient _tcpClient;
        private Channel _createdChannel;

        public Point(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
        }

        public IChannel CreateChannel()
        {
            if (_createdChannel is not null)
            {
                return _createdChannel;
            }

            var networkStream = _tcpClient.GetStream();
            _createdChannel = new Channel(networkStream);

            return _createdChannel;
        }

        public void Delete() => Dispose();

        public void Dispose()
        {
            _tcpClient.Dispose();
            _tcpClient = null;
            _createdChannel.Dispose();
            _createdChannel = null;
        }
    }
}