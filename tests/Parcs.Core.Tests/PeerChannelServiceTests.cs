using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Parcs.Core.Configuration;
using Parcs.Core.Services;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Core.Tests
{
    public class PeerChannelServiceTests
    {
        private CallbackTcpServer _callbackServer = null!;
        private PeerChannelService _service = null!;

        [SetUp]
        public async Task SetUp()
        {
            var configuration = new HostTcpConfiguration { Port = GetFreePort(), AdvertisedAddress = IPAddress.Loopback.ToString() };
            _callbackServer = new CallbackTcpServer(Options.Create(configuration), NullLogger<CallbackTcpServer>.Instance);
            await _callbackServer.StartAsync(CancellationToken.None);
            _service = new PeerChannelService(_callbackServer, NullLogger<PeerChannelService>.Instance);
        }

        [TearDown]
        public Task TearDown() => _callbackServer.StopAsync(CancellationToken.None);

        [Test]
        public async Task ConnectedPeersExchangeDataBothWays()
        {
            await using var endpoint = _service.OpenEndpoint(CancellationToken.None);

            var accept = endpoint.AcceptAsync();
            using var dialer = await _service.ConnectAsync(endpoint.Address, CancellationToken.None);
            using var listener = await accept.WaitAsync(TimeSpan.FromSeconds(5));

            await dialer.WriteDataAsync("ping");
            await listener.WriteDataAsync(42);

            Assert.Multiple(async () =>
            {
                Assert.That(await listener.ReadStringAsync(), Is.EqualTo("ping"));
                Assert.That(await dialer.ReadIntAsync(), Is.EqualTo(42));
            });
        }

        [Test]
        public async Task EachEndpointConnectsItsOwnPeer()
        {
            await using var first = _service.OpenEndpoint(CancellationToken.None);
            await using var second = _service.OpenEndpoint(CancellationToken.None);

            // Dial in the opposite order; each channel must still reach the endpoint it names.
            using var toSecond = await _service.ConnectAsync(second.Address, CancellationToken.None);
            using var toFirst = await _service.ConnectAsync(first.Address, CancellationToken.None);
            await toFirst.WriteDataAsync(1);
            await toSecond.WriteDataAsync(2);

            using var atFirst = await first.AcceptAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var atSecond = await second.AcceptAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(async () =>
            {
                Assert.That(await atFirst.ReadIntAsync(), Is.EqualTo(1));
                Assert.That(await atSecond.ReadIntAsync(), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task AnEndpointAcceptsOnlyOnce()
        {
            await using var endpoint = _service.OpenEndpoint(CancellationToken.None);
            _ = endpoint.AcceptAsync();

            Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.AcceptAsync());
        }

        [Test]
        public async Task DisposingAnEndpointCancelsItsAccept()
        {
            var endpoint = _service.OpenEndpoint(CancellationToken.None);
            var accept = endpoint.AcceptAsync();

            await endpoint.DisposeAsync();
            await endpoint.DisposeAsync();

            Assert.That(async () => await accept, Throws.InstanceOf<OperationCanceledException>());
        }

        [TestCase("10.0.3.17:2222/abc", "10.0.3.17", 2222, "abc")]
        [TestCase("parcs-daemon-0.parcs:2222/f00", "parcs-daemon-0.parcs", 2222, "f00")]
        public void ParsesAddresses(string address, string host, int port, string id) =>
            Assert.That(PeerAddress.Parse(address), Is.EqualTo((host, port, id)));

        [TestCase("")]
        [TestCase("10.0.3.17:2222")]
        [TestCase("10.0.3.17/abc")]
        [TestCase("10.0.3.17:x/abc")]
        [TestCase("10.0.3.17:2222/")]
        public void RejectsMalformedAddresses(string address) =>
            Assert.Throws<FormatException>(() => PeerAddress.Parse(address));

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
