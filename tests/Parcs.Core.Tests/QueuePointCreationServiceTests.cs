using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Parcs.Core.Configuration;
using Parcs.Core.Messaging;
using Parcs.Core.Models;
using Parcs.Core.Models.Interfaces;
using Parcs.Core.Services;
using Parcs.Core.Services.Interfaces;
using Parcs.Net;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Core.Tests
{
    public class QueuePointCreationServiceTests
    {
        private CallbackTcpServer _callbackServer = null!;
        private HostTcpConfiguration _hostTcpConfiguration = null!;

        [SetUp]
        public async Task SetUp()
        {
            _hostTcpConfiguration = new HostTcpConfiguration
            {
                Port = GetFreePort(),
                AdvertisedAddress = IPAddress.Loopback.ToString(),
                DaemonConnectTimeoutSeconds = 10,
            };

            _callbackServer = new CallbackTcpServer(Options.Create(_hostTcpConfiguration), NullLogger<CallbackTcpServer>.Instance);
            await _callbackServer.StartAsync(CancellationToken.None);
        }

        [TearDown]
        public Task TearDown() => _callbackServer.StopAsync(CancellationToken.None);

        [TestCase(false)]
        [TestCase(true)]
        public async Task CreatesOnePointPerConnectedDaemon(bool requiresGpu)
        {
            var publisher = new DaemonSimulatingPublisher();
            var service = CreateService(publisher, requiresGpu);

            var points = await service.CreatePointsAsync(3, jobId: 7, moduleId: 11, new Dictionary<string, string> { ["N"] = "100" });

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Length.EqualTo(3));
                Assert.That(publisher.Published, Has.Count.EqualTo(3));
                Assert.That(publisher.RequiresGpu, Is.EqualTo(requiresGpu));
                Assert.That(publisher.Published.Select(r => r.CorrelationId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(publisher.Published, Has.All.Matches<PointCreationRequest>(r =>
                    r.JobId == 7 && r.ModuleId == 11 && r.RequiresGpu == requiresGpu &&
                    r.HostUrl == IPAddress.Loopback.ToString() && r.HostPort == _hostTcpConfiguration.Port &&
                    r.RequestedAt != null));
            });
        }

        [Test]
        public void TimesOutWhenDaemonsNeverConnect()
        {
            _hostTcpConfiguration.DaemonConnectTimeoutSeconds = 1;
            var publisher = Substitute.For<IPointRequestPublisher>();
            publisher.IsEnabled.Returns(true);

            var service = CreateService(publisher, requiresGpu: false);

            Assert.ThrowsAsync<TimeoutException>(() => service.CreatePointsAsync(2, 1, 1, new Dictionary<string, string>()));
        }

        [Test]
        public void IsDisabledWithoutBroker()
        {
            Assert.That(CreateService(new DisabledPointQueue(), requiresGpu: false).IsEnabled, Is.False);
        }

        // The recursion fix: a module running on a daemon (it has a parent channel, so IsHost is
        // false) must create child points through the point queue, not through the static daemon
        // list, which is empty when daemons are provisioned by KEDA.
        [Test]
        public async Task ModuleOnDaemonCreatesNestedPointsThroughQueue()
        {
            var pointCreationService = Substitute.For<IPointCreationService>();
            pointCreationService.IsEnabled.Returns(true);
            pointCreationService
                .CreatePointAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IPoint>(new Point(5, 6, Substitute.For<IManagedChannel>(), Substitute.For<IArgumentsProvider>())));

            var daemonResolver = Substitute.For<IDaemonResolver>();

            await using var moduleInfo = new ModuleInfo(
                new JobMetadata(5, 6),
                parentChannel: Substitute.For<IChannel>(),
                Substitute.For<IInputOutputFactory>(),
                Substitute.For<IArgumentsProvider>(),
                daemonResolver,
                Substitute.For<IInternalChannelManager>(),
                Substitute.For<IAddressResolver>(),
                NullLogger.Instance,
                CancellationToken.None,
                pointCreationService);

            Assert.That(moduleInfo.IsHost, Is.False);

            await moduleInfo.CreatePointAsync();

            await pointCreationService.Received(1).CreatePointAsync(5, 6, Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>());
            daemonResolver.DidNotReceiveWithAnyArgs().GetAvailableDaemons();
        }

        private QueuePointCreationService CreateService(IPointRequestPublisher publisher, bool requiresGpu)
        {
            var placementResolver = Substitute.For<IJobPlacementResolver>();
            placementResolver.RequiresGpuAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(requiresGpu);

            return new QueuePointCreationService(
                publisher,
                placementResolver,
                _callbackServer,
                Options.Create(_hostTcpConfiguration),
                new ArgumentsProviderFactory(),
                NullLogger<QueuePointCreationService>.Instance);
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        /// <summary>
        /// Plays the role of broker + KEDA + daemon: every published request results in a
        /// "daemon" dialling back to the callback server and completing the handshake.
        /// </summary>
        private sealed class DaemonSimulatingPublisher : IPointRequestPublisher
        {
            private readonly List<TcpClient> _daemons = [];

            public List<PointCreationRequest> Published { get; } = [];

            public bool? RequiresGpu { get; private set; }

            public bool IsEnabled => true;

            public async Task PublishAsync(IReadOnlyList<PointCreationRequest> requests, bool requiresGpu, CancellationToken cancellationToken = default)
            {
                RequiresGpu = requiresGpu;
                Published.AddRange(requests);

                foreach (var request in requests)
                {
                    var daemon = new TcpClient();
                    await daemon.ConnectAsync(request.HostUrl, request.HostPort, cancellationToken);
                    _daemons.Add(daemon);

                    var channel = new NetworkChannel(daemon);
                    await channel.WriteDataAsync(request.CorrelationId);
                }
            }
        }
    }
}
