using NetCoreServer;
using Parcs.Core;

namespace Parcs.TCP.Host.Models
{
    internal class Point : IPoint
    {
        private readonly TcpSession _tcpSession;

        public Point(TcpSession tcpSession)
        {
            _tcpSession = tcpSession;
        }

        public IChannel CreateChannel()
        {
            return new Channel(_tcpSession);
        }
    }
}