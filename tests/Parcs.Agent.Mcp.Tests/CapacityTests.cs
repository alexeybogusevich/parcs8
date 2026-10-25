using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Agent.Mcp.Services;

namespace Parcs.Agent.Mcp.Tests;

public class CapacityTests
{
    [Test]
    public async Task GateAdmitsInStrictFifoOrder()
    {
        var gate = new CapacityGate(initialCapacity: 4);

        var a = await gate.AcquireAsync(3, CancellationToken.None);
        var b = gate.AcquireAsync(3, CancellationToken.None);
        var c = gate.AcquireAsync(1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(b.IsCompleted, Is.False);
            Assert.That(c.IsCompleted, Is.False, "a narrow layer does not overtake a queued wide one");
        });

        a.Dispose();
        Assert.Multiple(() =>
        {
            Assert.That(b.IsCompletedSuccessfully, Is.True);
            Assert.That(c.IsCompletedSuccessfully, Is.True);
            Assert.That(gate.InUse, Is.EqualTo(4));
        });

        (await b).Dispose();
        (await c).Dispose();
        (await c).Dispose();   // a second dispose frees nothing
        Assert.That(gate.InUse, Is.Zero);
    }

    [Test]
    public async Task CancelledWaiterLeavesTheQueueAndUnblocksTheNext()
    {
        var gate = new CapacityGate(initialCapacity: 2);
        var held = await gate.AcquireAsync(1, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var wide = gate.AcquireAsync(2, cts.Token);
        var narrow = gate.AcquireAsync(1, CancellationToken.None);
        cts.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(wide.IsCanceled, Is.True);
            Assert.That(narrow.IsCompletedSuccessfully, Is.True);
            Assert.That(gate.Queued, Is.Zero);
        });
        held.Dispose();
    }

    [Test]
    public async Task RaisingCapacityAdmitsWaiters()
    {
        var gate = new CapacityGate(initialCapacity: 1);
        var held = await gate.AcquireAsync(1, CancellationToken.None);
        var waiting = gate.AcquireAsync(2, CancellationToken.None);

        gate.SetCapacity(3);

        Assert.That(waiting.IsCompletedSuccessfully, Is.True);
        held.Dispose();
    }

    [Test]
    public async Task StaticModeCountsReadyDaemonsOfTheHeadlessService()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Scaling:Mode"] = "Static",
            ["Scaling:PointsPerDaemon"] = "2",
            ["Kubernetes:DaemonsHeadlessServiceName"] = "parcs-daemon-headless",
            ["Kubernetes:NamespaceName"] = "default",
        }).Build();
        string? resolvedHost = null;
        var service = new ClusterInfoService(NullLogger<ClusterInfoService>.Instance, configuration, (host, _) =>
        {
            resolvedHost = host;
            return Task.FromResult(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2"), IPAddress.Parse("10.0.0.3") });
        });

        var info = await service.GetClusterInfoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resolvedHost, Is.EqualTo("parcs-daemon-headless.default"));
            Assert.That(info.ScalingMode, Is.EqualTo(ScalingModes.Static));
            Assert.That(info.CapacitySource, Is.EqualTo(CapacitySources.Dns));
            Assert.That(info.MaxParallelism, Is.EqualTo(6));
            Assert.That(info.CurrentCapacity, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task StaticModeOutsideKubernetesUsesTheConfiguredPoolSize()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Scaling:Mode"] = "static",
            ["Scaling:StaticDaemonCount"] = "4",
        }).Build();
        var service = new ClusterInfoService(NullLogger<ClusterInfoService>.Instance, configuration);

        var info = await service.GetClusterInfoAsync();

        Assert.That(info, Has.Property(nameof(ClusterInfoResult.MaxParallelism)).EqualTo(4)
                             .And.Property(nameof(ClusterInfoResult.CapacitySource)).EqualTo(CapacitySources.Configuration));
    }

    [Test]
    public void UnknownScalingModeIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scaling:Mode"] = "turbo" }).Build();

        Assert.Throws<InvalidOperationException>(() => ScalingModes.Resolve(configuration));
    }
}
