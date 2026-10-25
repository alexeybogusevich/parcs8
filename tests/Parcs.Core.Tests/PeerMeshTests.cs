using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Parcs.Agent.Runtime;
using Parcs.Core.Configuration;
using Parcs.Core.Services;
using Parcs.Modules.AgentRunner;
using System.Net;
using System.Net.Sockets;

namespace Parcs.Core.Tests
{
    /// <summary>On-demand peer channels between the workers of a layer, over real loopback sockets.</summary>
    public class PeerMeshTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
        public async Task OnlyTheChannelsTheCodeAsksForAreOpened()
        {
            var meshes = Meshes(4);
            try
            {
                // A line 0-1-2, worker 3 talks to nobody.
                await meshes[0][1].SendAsync("a");
                await meshes[2][1].SendAsync("c");
                var fromZero = await meshes[1][0].ReceiveAsync();
                var fromTwo = await meshes[1][2].ReceiveAsync();

                Assert.Multiple(() =>
                {
                    Assert.That((fromZero, fromTwo), Is.EqualTo(("a", "c")));
                    Assert.That(meshes[1].Connected, Is.EqualTo(new[] { 0, 2 }));
                    Assert.That(meshes[0].Connected, Is.EqualTo(new[] { 1 }));
                    Assert.That(meshes[3].Connected, Is.Empty);
                });
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task EveryPairDialingAtOnceEndsUpWithOneChannel()
        {
            // All workers open channels to all others at the same moment: every pair races.
            const int count = 6;
            var meshes = Meshes(count);
            try
            {
                await Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(async () =>
                {
                    foreach (var j in Enumerable.Range(0, count).Where(j => j != i))
                        await meshes[i][j].SendAsync($"{i}->{j}");
                })));

                var received = await Task.WhenAll(Enumerable.Range(0, count).SelectMany(i =>
                    Enumerable.Range(0, count).Where(j => j != i).Select(async j => (i, j, message: await meshes[i][j].ReceiveAsync()))));

                Assert.Multiple(() =>
                {
                    foreach (var (i, j, message) in received)
                        Assert.That(message, Is.EqualTo($"{j}->{i}"));
                    for (var i = 0; i < count; i++)
                        Assert.That(meshes[i].Connected, Has.Count.EqualTo(count - 1), $"worker {i}");
                });

                // No duplicate channel carried a second copy of anything.
                foreach (var (i, j, _) in received)
                    Assert.ThrowsAsync<TimeoutException>(() => meshes[i][j].ReceiveAsync(TimeSpan.FromMilliseconds(50)));
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task AWorkerLearnsItsPartnerFromWhoeverContactsIt()
        {
            var meshes = Meshes(3);
            try
            {
                // Worker 2 picks worker 0; worker 0 does not know who will call.
                await meshes[2][0].SendAsync("hello from 2");
                var channel = await meshes[0].AcceptAsync();
                var message = await channel.ReceiveAsync();
                await channel.SendAsync("hi 2");

                Assert.Multiple(async () =>
                {
                    Assert.That((channel.Peer, message), Is.EqualTo((2, "hello from 2")));
                    Assert.That(await meshes[2][0].ReceiveAsync(), Is.EqualTo("hi 2"));
                    Assert.That(() => meshes[0].AcceptAsync(TimeSpan.FromMilliseconds(100)),
                        Throws.InstanceOf<TimeoutException>().With.Message.Contains("channel_timeout"));
                });
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task AChannelAskedForByIndexIsNotAcceptedAgain()
        {
            var meshes = Meshes(2);
            try
            {
                await meshes[1][0].SendAsync("x");
                Assert.That(await meshes[0][1].ReceiveAsync(), Is.EqualTo("x"));

                Assert.That(() => meshes[0].AcceptAsync(TimeSpan.FromMilliseconds(200)), Throws.InstanceOf<TimeoutException>());
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task FailuresAreReportedOnTheChannel()
        {
            var meshes = Meshes(3, missing: 2);
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(() => meshes[0][2].ReceiveAsync(),
                        Throws.InstanceOf<IOException>().With.Message.Contains("no endpoint"));
                    Assert.That(() => meshes[0][1].ReceiveAsync(TimeSpan.FromMilliseconds(200)),
                        Throws.InstanceOf<TimeoutException>().With.Message.Contains("channel_timeout"));
                    Assert.That(() => meshes[0][0], Throws.ArgumentException);
                    Assert.That(() => meshes[0][3], Throws.InstanceOf<ArgumentOutOfRangeException>());
                });

                // A worker that goes away closes its channels: the other end fails at once.
                await ((PeerMesh)meshes[1]).DisposeAsync();
                Assert.That(() => meshes[0][1].ReceiveAsync(), Throws.InstanceOf<IOException>());
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task LargeMessagesBothWaysDoNotDeadlock()
        {
            var meshes = Meshes(2);
            try
            {
                var big = new string('z', 8 * 1024 * 1024);
                await meshes[0][1].SendAsync(big);
                await meshes[1][0].SendAsync(big);

                var lengths = await Task.WhenAll(meshes[0][1].ReceiveAsync(), meshes[1][0].ReceiveAsync()).WaitAsync(Timeout);
                Assert.That(lengths.Select(m => m.Length), Is.All.EqualTo(big.Length));
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task ProbingNeverWaits()
        {
            var meshes = Meshes(2);
            try
            {
                var channel = meshes[0][1];
                Assert.Multiple(() =>
                {
                    Assert.That(channel.HasMessage, Is.False);
                    Assert.That(channel.TryReceive(out _), Is.False);
                });

                await meshes[1][0].SendAsync("x");
                await PollAsync(() => channel.HasMessage);

                Assert.Multiple(() =>
                {
                    Assert.That(channel.TryReceive(out var message), Is.True);
                    Assert.That(message, Is.EqualTo("x"));
                    Assert.That(channel.HasMessage, Is.False);
                });
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task SelectiveWaitTakesWhoeverSendsFirst()
        {
            var meshes = Meshes(4);
            try
            {
                var waiting = meshes[0].ReceiveAnyAsync([1, 2, 3]);
                await Task.Delay(200);
                Assert.That(waiting.IsCompleted, Is.False, "nobody has sent yet");

                await meshes[2][0].SendAsync("from 2");
                var (peer, message) = await waiting.WaitAsync(Timeout);
                Assert.That((peer, message), Is.EqualTo((2, "from 2")));

                // With several waiting, the first named wins and the rest stay for later.
                await meshes[3][0].SendAsync("from 3");
                await meshes[1][0].SendAsync("from 1");
                await PollAsync(() => meshes[0][1].HasMessage && meshes[0][3].HasMessage);
                Assert.Multiple(async () =>
                {
                    Assert.That(await meshes[0].ReceiveAnyAsync([3, 1]), Is.EqualTo((3, "from 3")));
                    Assert.That(await meshes[0].ReceiveAnyAsync([3, 1]), Is.EqualTo((1, "from 1")));
                    Assert.That(() => meshes[0].ReceiveAnyAsync([1, 2, 3], TimeSpan.FromMilliseconds(100)),
                        Throws.InstanceOf<TimeoutException>().With.Message.Contains("any of workers 1, 2, 3"));
                });
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        [Test]
        public async Task SelectiveWaitFailsOnlyWhenEveryPartnerIsGone()
        {
            var meshes = Meshes(3);
            try
            {
                _ = meshes[1][0];
                _ = meshes[2][0];
                await PollAsync(() => meshes[0].Connected.Count == 2);

                var waiting = meshes[0].ReceiveAnyAsync([1, 2]);
                await ((PeerMesh)meshes[1]).DisposeAsync();
                await Task.Delay(200);
                Assert.That(waiting.IsCompleted, Is.False, "worker 2 may still send");

                await meshes[2][0].SendAsync("still here");
                Assert.That(await waiting.WaitAsync(Timeout), Is.EqualTo((2, "still here")));

                await ((PeerMesh)meshes[2]).DisposeAsync();
                Assert.That(() => meshes[0].ReceiveAnyAsync([1, 2]), Throws.InstanceOf<IOException>());
            }
            finally
            {
                await DisposeAsync(meshes);
            }
        }

        private static async Task PollAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("Timed out waiting for the condition.");
                await Task.Delay(10);
            }
        }

        /// <summary>One mesh per worker, wired as the main module wires them; <paramref name="missing"/> has no endpoint.</summary>
        private PeerMesh?[] MeshesOrNull(int count, int missing)
        {
            var endpoints = Enumerable.Range(0, count)
                .Select(i => i == missing ? null : _service.OpenEndpoint(CancellationToken.None))
                .ToArray();
            var directory = endpoints.Select(e => e?.Address).ToList();

            return [.. endpoints.Select((endpoint, i) => endpoint is null ? null
                : new PeerMesh(i, directory, endpoint, address => _service.ConnectAsync(address, CancellationToken.None), Timeout))];
        }

        private IAgentPeers[] Meshes(int count, int missing = -1) =>
            [.. MeshesOrNull(count, missing).Select(m => (IAgentPeers?)m ?? new UnavailableAgentPeers("missing"))];

        private static async Task DisposeAsync(IAgentPeers[] meshes)
        {
            foreach (var mesh in meshes.OfType<PeerMesh>())
                await mesh.DisposeAsync();
        }

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
