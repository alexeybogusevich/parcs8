using Parcs.Core.Services.Interfaces;
using System.Net;

namespace Parcs.Core.Services
{
    public class AddressResolver : IAddressResolver
    {
        public IPAddress[] Resolve(string hostName)
        {
            var otherHostAddresses = Dns.GetHostAddresses(hostName);

            var currentHostName = Dns.GetHostName();
            var currentHostAddresses = Dns.GetHostAddresses(currentHostName).ToList();

            if (currentHostAddresses.Any(currentAddress => otherHostAddresses.Contains(currentAddress)))
            {
                return new IPAddress[] { IPAddress.Parse("127.0.0.1") };
            }

            return otherHostAddresses;
        }
    }
}