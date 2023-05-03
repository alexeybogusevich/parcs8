using Parcs.Core.Services.Interfaces;
using System.Net.Sockets;
using System.Net;

namespace Parcs.Core.Services
{
    public class AddressResolver : IAddressResolver
    {
        public IPAddress[] Resolve(string url)
        {
            var addresses = Dns.GetHostAddresses(url);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);

            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            var localIP = endPoint.Address;

            if (addresses.Any(a => a.Equals(localIP)))
            {
                return new IPAddress[] { IPAddress.Parse("127.0.0.1") };
            }

            return addresses;
        }
    }
}