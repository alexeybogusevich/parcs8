using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Core.Configuration;
using Parcs.Core.Messaging;
using Parcs.Core.Models;
using Parcs.Core.Services;

namespace Parcs.Core.Tests
{
    public class ScalingModeTests
    {
        private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
                .Build();

        [Test]
        public void ModeFollowsTheQueueWhenUnset()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PointQueueServiceCollectionExtensions.ResolveScalingMode(Configuration()), Is.EqualTo(ScalingModes.Static));
                Assert.That(PointQueueServiceCollectionExtensions.ResolveScalingMode(Configuration(("PubSub:ProjectId", "parcs-gcp"))),
                    Is.EqualTo(ScalingModes.Elastic));
            });
        }

        [TestCase("static", ScalingModes.Static)]
        [TestCase("Elastic", ScalingModes.Elastic)]
        public void ExplicitModeIsCaseInsensitive(string value, string expected)
        {
            Assert.That(PointQueueServiceCollectionExtensions.ResolveScalingMode(Configuration(("Scaling:Mode", value))), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownModeIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PointQueueServiceCollectionExtensions.ResolveScalingMode(Configuration(("Scaling:Mode", "Burst"))));
        }

        [Test]
        public async Task StaticModeDisablesAConfiguredQueue()
        {
            await using var serviceProvider = new ServiceCollection()
                .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
                .AddPointQueue(Configuration(("Scaling:Mode", "Static"), ("PointQueue:Provider", "PubSub"), ("PubSub:ProjectId", "parcs-gcp")))
                .BuildServiceProvider();

            Assert.Multiple(() =>
            {
                Assert.That(serviceProvider.GetRequiredService<IPointRequestPublisher>(), Is.TypeOf<DisabledPointQueue>());
                Assert.That(serviceProvider.GetRequiredService<IPointRequestReceiver>().IsEnabled, Is.False);
            });
        }

        [Test]
        public void AllocatorSpreadsConcurrentJobsOverThePool()
        {
            var allocator = new DaemonAllocator();
            var pool = Enumerable.Range(1, 3).Select(i => new Daemon { HostUrl = $"10.0.0.{i}", Port = 1111 }).ToList();

            // Two jobs of two points each, as two ModuleInfo instances would place them.
            var job1 = new[] { allocator.Acquire(pool), allocator.Acquire(pool) };
            var job2 = new[] { allocator.Acquire(pool), allocator.Acquire(pool) };

            var hosts = job1.Concat(job2).Select(l => l.Daemon.HostUrl).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(hosts.Take(3), Is.Unique, "the first three points take three different daemons");
                Assert.That(allocator.ActivePoints.Values.Max(), Is.EqualTo(2));
            });

            foreach (var lease in job1) lease.Dispose();
            job1[0].Dispose();   // a second dispose is a no-op

            Assert.That(allocator.ActivePoints.Values.Sum(), Is.EqualTo(2));

            // The next point goes to a daemon that job 2 is not using.
            var busy = job2.Select(l => l.Daemon.HostUrl).ToHashSet();
            if (busy.Count == 2)
            {
                Assert.That(busy, Does.Not.Contain(allocator.Acquire(pool).Daemon.HostUrl));
            }
        }

        [Test]
        public void AllocatorRejectsAnEmptyPool()
        {
            Assert.Throws<InvalidOperationException>(() => new DaemonAllocator().Acquire([]));
        }
    }
}
