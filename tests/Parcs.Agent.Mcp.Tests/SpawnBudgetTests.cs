using System.Text.Json;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

namespace Parcs.Agent.Mcp.Tests;

/// <summary>maxPoints and maxDepth: the points budget of layers whose workers spawn.</summary>
public class SpawnBudgetTests
{
    [Test]
    public async Task WithoutABudgetNothingSpawnsAndNothingChanges()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 4 });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.Multiple(() =>
        {
            Assert.That((layer.MaxPoints, layer.MaxDepth, layer.Slots), Is.EqualTo((4, 0, 4)));
            Assert.That(input.RootElement.GetProperty("MaxPoints").GetInt32(), Is.EqualTo(4));
            Assert.That(LayerResponses.Build(layer, null).Layer!.MaxPoints, Is.Null, "no budget to report");
        });
    }

    [Test]
    public async Task ABudgetReachesTheJobWithADefaultDepth()
    {
        var harness = new Harness(maxParallelism: 4);
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2, MaxPoints = 14 });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("MaxPoints").GetInt32(), Is.EqualTo(14));
            Assert.That(input.RootElement.GetProperty("MaxDepth").GetInt32(), Is.EqualTo(SessionManager.DefaultSpawnDepth));
            Assert.That(layer.Slots, Is.EqualTo(4), "a static pool holds every daemon, not one slot per point");
            Assert.That(LayerResponses.Build(layer, null).Layer!.MaxPoints, Is.EqualTo(14));
        });
    }

    [TestCase(4, 3, null, "at least parallelism")]
    [TestCase(4, 8, 0, "maxDepth of at least 1")]
    [TestCase(4, 8, 9, "between 0 and 8")]
    public async Task InconsistentBudgetsAreRejected(int parallelism, int maxPoints, int? maxDepth, string reason)
    {
        var harness = new Harness();
        var started = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = parallelism, MaxPoints = maxPoints, MaxDepth = maxDepth,
        });

        Assert.Multiple(() =>
        {
            Assert.That(started.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(started.Error.Message, Does.Contain(reason));
            Assert.That(harness.Api.JobsCreated, Is.Zero);
        });
    }

    [TestCase(ScalingModes.Static, 16, null)]
    [TestCase(ScalingModes.Static, 17, ErrorCodes.CapacityExceeded)]
    [TestCase(ScalingModes.Elastic, 4, null)]
    [TestCase(ScalingModes.Elastic, 5, ErrorCodes.CapacityExceeded)]
    public async Task TheBudgetMustFitTheCluster(string mode, int maxPoints, string? error)
    {
        // Static pools host several points per daemon; elastic clusters give every point a pod.
        var harness = new Harness(maxParallelism: 4);
        harness.Cluster.ScalingMode = mode;

        var started = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 2, MaxPoints = maxPoints,
        });

        Assert.That(started.Error?.Code, Is.EqualTo(error));
    }

    [Test]
    public async Task TheBudgetIsPartOfTheLayerKeyButOnlyWhenSpawning()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession();
        await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, LayerKey = "plain" });
        await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, LayerKey = "spawn", MaxPoints = 6 });

        var plainAgain = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, LayerKey = "plain", MaxPoints = 2 });
        var otherBudget = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, LayerKey = "spawn", MaxPoints = 8 });

        Assert.Multiple(() =>
        {
            Assert.That(plainAgain.Reused, Is.True, "maxPoints equal to parallelism is no budget at all");
            Assert.That(otherBudget.Error!.Code, Is.EqualTo(ErrorCodes.LayerKeyConflict));
        });
    }

    [Test]
    public async Task SpawnedPointsAreCountedInTheSummary()
    {
        var harness = new Harness();
        harness.Api.Output = _ => """
            {"results":[
              {"workerIndex":0,"success":true,"outputData":"1","spawned":6},
              {"workerIndex":1,"success":true,"outputData":"2","spawned":2}]}
            """;
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2, MaxPoints = 10 });

        var full = LayerResponses.Build(layer, "full");
        Assert.Multiple(() =>
        {
            Assert.That(full.Layer!.SpawnedPoints, Is.EqualTo(8));
            Assert.That(full.Workers![0].Spawned, Is.EqualTo(6));
        });
    }
}
