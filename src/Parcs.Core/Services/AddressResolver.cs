using Parcs.Core.Services.Interfaces;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Parcs.Core.Services
{
    public class AddressResolver : IAddressResolver
    {
        private readonly ILogger<AddressResolver> _logger;

        public AddressResolver(ILogger<AddressResolver> logger)
        {
            _logger = logger;
        }

        public IPAddress[] Resolve(string hostName)
        {
            var otherHostAddresses = Dns.GetHostAddresses(hostName);

            _logger.LogInformation(
                "Resolved host {HostName} to IP addresses: {Addresses}", hostName, string.Join(',', otherHostAddresses.Select(a => a.ToString())));

            var currentHostName = Dns.GetHostName();
            var currentHostAddresses = Dns.GetHostAddresses(currentHostName).ToList();

            _logger.LogInformation(
                "Resolved current host {HostName} to IP addresses: {Addresses}", currentHostName, string.Join(',', currentHostAddresses.Select(a => a.ToString())));

            if (currentHostAddresses.Any(currentAddress => otherHostAddresses.Contains(currentAddress)))
            {
                _logger.LogInformation("{HostName} to be treated as loopback", hostName);
                return new IPAddress[] { IPAddress.Parse("127.0.0.1") };
            }

            _logger.LogInformation("{HostName} not to be treated as loopback", hostName);

            return otherHostAddresses;
        }
    }
}