using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;

namespace Parcs.Core.Services
{
    public class AddressResolver : IAddressResolver
    {
        public bool IsSameAddressAsHost(string url)
        {
            var addresses = Dns.GetHostAddresses(url);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);

            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            var localIP = endPoint.Address;

            return addresses.Any(a => a.Equals(localIP));
        }
    }
}