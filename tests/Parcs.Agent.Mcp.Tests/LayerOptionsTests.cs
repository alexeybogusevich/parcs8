using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

namespace Parcs.Agent.Mcp.Tests;

/// <summary>Topology, inherit, delete_session and the session store.</summary>
public class LayerOptionsTests
{
    // ─── Topology ────────────────────────────────────────────────────

    [Test]
    public async Task TopologyReachesTheJobAndFailsFastByDefault()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 6, Topology = "Grid" });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        var envelope = LayerResponses.Build(layer, null);
        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("Topology").GetString(), Is.EqualTo("grid:2x3"));
            Assert.That(input.RootElement.GetProperty("ChannelTimeoutSeconds").GetInt32(), Is.EqualTo(60));
            Assert.That(input.RootElement.GetProperty("FailFast").GetBoolean(), Is.True);
            Assert.That(envelope.Layer!.Topology, Is.EqualTo("grid:2x3"));
        });
    }

    [Test]
    public async Task ExplicitFailFastOverridesTheTopologyDefault()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 4, Topology = "ring", FailFast = false, ChannelTimeoutSeconds = 5,
        });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("FailFast").GetBoolean(), Is.False);
            Assert.That(input.RootElement.GetProperty("ChannelTimeoutSeconds").GetInt32(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task CustomEdgesAreSentWithTheLayer()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 3, Topology = "custom", TopologyEdges = [[0, 2], [2, 1]],
        });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.That(input.RootElement.GetProperty("TopologyEdges")[1][0].GetInt32(), Is.EqualTo(2));
    }

    [TestCase("hypercube", 6, null)]
    [TestCase("donut", 4, null)]
    [TestCase("ring", 4, 0)]
    [TestCase("ring", 4, 901)]
    public async Task InvalidTopologyArgumentsAreRejected(string topology, int parallelism, int? channelTimeout)
    {
        var harness = new Harness();
        var started = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = parallelism, Topology = topology, ChannelTimeoutSeconds = channelTimeout,
        });

        Assert.Multiple(() =>
        {
            Assert.That(started.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(harness.Api.JobsCreated, Is.Zero);
        });
    }

    [Test]
    public async Task LayerKeyDistinguishesTopologies()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession();
        await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 4, LayerKey = "k", Topology = "ring" });

        var same = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 4, LayerKey = "k", Topology = "RING" });
        var other = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 4, LayerKey = "k", Topology = "mesh" });

        Assert.Multiple(() =>
        {
            Assert.That(same.Reused, Is.True, "the same topology in another spelling is the same layer");
            Assert.That(other.Error!.Code, Is.EqualTo(ErrorCodes.LayerKeyConflict));
        });
    }

    // ─── Inherit ─────────────────────────────────────────────────────

    [Test]
    public async Task ByIndexSendsEachWorkerOnlyItsOwnParentOutputs()
    {
        var harness = new Harness();
        harness.Api.Output = _ => FakeParcsApi.Results((true, "a0"), (false, "boom"), (true, "a2"));
        var sessionId = harness.NewSession(Harness.MapReduceSource);
        var parent = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 3, EntryPoint = "Map" });

        var child = await harness.RunAsync(new LayerRequest
        {
            SessionId = sessionId, Parallelism = 3, EntryPoint = "Reduce", PreviousLayerIds = [parent.LayerId], Inherit = "BYINDEX",
        });

        var files = harness.Api.InputFiles[child.JobId!.Value];
        using var input = JsonDocument.Parse(files["layer_input.json"]);
        string?[] Inherited(int i) =>
            [.. JsonDocument.Parse(files[$"worker_{i}.json"]).RootElement.GetProperty("InheritedOutputs").EnumerateArray().Select(e => e.GetString())];

        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("PreviousLayerResults").GetArrayLength(), Is.Zero, "no whole documents with byIndex");
            Assert.That(input.RootElement.GetProperty("Inherit").GetString(), Is.EqualTo("byIndex"));
            Assert.That(Inherited(0), Is.EqualTo(new[] { "a0" }));
            Assert.That(Inherited(1), Is.EqualTo(new string?[] { null }), "a failed parent worker inherits null");
            Assert.That(Inherited(2), Is.EqualTo(new[] { "a2" }));
            Assert.That(LayerResponses.Build(child, null).Layer!.Inherit, Is.EqualTo("byIndex"));
        });
    }

    [Test]
    public async Task ByIndexNeedsParentsOfTheSameParallelism()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession();
        var parent = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2 });

        var mismatch = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = sessionId, Parallelism = 3, PreviousLayerIds = [parent.LayerId], Inherit = "byIndex",
        });
        var noParents = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, Inherit = "byIndex" });
        var unknown = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, Inherit = "some" });

        Assert.Multiple(() =>
        {
            Assert.That(mismatch.Error!.Message, Does.Contain("parallelism 3"));
            Assert.That(noParents.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(unknown.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
        });
    }

    [Test]
    public async Task LayersWithoutPerWorkerDataUseNoWorkerFiles()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2 });

        Assert.That(harness.Api.InputFiles[layer.JobId!.Value].Keys, Is.EquivalentTo(new[] { "layer_input.json" }));
    }

    // ─── delete_session ──────────────────────────────────────────────

    [Test]
    public async Task DeletingASessionCancelsAndRemovesItsLayers()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession();
        var done = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, LayerKey = "a" });
        harness.Api.HoldRuns = true;
        var running = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1 }, waitForCompletion: false);
        await Harness.EventuallyAsync(() => running.JobId is not null, "the layer has a job");

        var deleted = await harness.Sessions.DeleteSessionAsync(sessionId);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(2));
            Assert.That(harness.Api.CancelledJobs, Does.Contain(running.JobId!.Value));
            Assert.That(harness.Sessions.GetSession(sessionId), Is.Null);
            Assert.That(harness.Sessions.GetLayer(done.LayerId), Is.Null);
            Assert.That(harness.Gate.InUse, Is.Zero);
        });
    }

    [Test]
    public async Task DeletedSourceCompilesIntoANewSession()
    {
        var harness = new Harness();
        var first = harness.NewSession();

        Assert.That(await harness.Sessions.DeleteSessionAsync(first), Is.Zero);
        Assert.That(await harness.Sessions.DeleteSessionAsync(first), Is.Null);
        Assert.That(harness.Sessions.CreateSession(Harness.BodySource).Reused, Is.False);
    }

    // ─── Session store ───────────────────────────────────────────────

    [Test]
    public async Task SessionsAndFinishedLayersSurviveARestart()
    {
        var root = Directory.CreateTempSubdirectory("parcs-sessions-").FullName;
        try
        {
            var before = new Harness(store: Store(root));
            var sessionId = before.NewSession();
            var layer = await before.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, LayerKey = "k", Topology = "ring" });

            var after = new Harness(store: Store(root));
            var restored = after.Sessions.GetLayer(layer.LayerId)!;
            var again = await after.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, LayerKey = "k", Topology = "ring" });
            var child = await after.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, PreviousLayerIds = [layer.LayerId] });

            Assert.Multiple(() =>
            {
                Assert.That(after.Sessions.GetSession(sessionId)!.Assembly, Is.EqualTo(before.Sessions.GetSession(sessionId)!.Assembly));
                Assert.That(after.Sessions.CreateSession(Harness.BodySource).Reused, Is.True);
                Assert.That(restored.Status, Is.EqualTo(LayerStatus.Completed));
                Assert.That(restored.Topology, Is.EqualTo("ring"));
                Assert.That(LayerResponses.Build(restored, null).Output!["value"]!.GetValue<int>(), Is.EqualTo(42));
                Assert.That(again.Reused, Is.True, "the layerKey still names the stored layer");
                Assert.That(child.Error, Is.Null, "a stored layer can be a parent");
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ALayerRunningAtShutdownIsRestoredAsFailed()
    {
        var root = Directory.CreateTempSubdirectory("parcs-sessions-").FullName;
        try
        {
            var before = new Harness(store: Store(root));
            before.Api.HoldRuns = true;
            var layer = await before.RunAsync(new LayerRequest { SessionId = before.NewSession(), Parallelism = 1 }, waitForCompletion: false);

            var restored = new Harness(store: Store(root)).Sessions.GetLayer(layer.LayerId)!;

            Assert.Multiple(() =>
            {
                Assert.That(restored.Status, Is.EqualTo(LayerStatus.Failed));
                Assert.That(restored.Error!.Retryable, Is.True);
                Assert.That(restored.Error.Message, Does.Contain("restarted"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task DeletingASessionRemovesItFromTheStore()
    {
        var root = Directory.CreateTempSubdirectory("parcs-sessions-").FullName;
        try
        {
            var harness = new Harness(store: Store(root));
            var sessionId = harness.NewSession();
            await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1 });

            await harness.Sessions.DeleteSessionAsync(sessionId);

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(Path.Combine(root, sessionId)), Is.False);
                Assert.That(new Harness(store: Store(root)).Sessions.ListSessions(), Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FileSessionStore Store(string root) => new(root, NullLogger<FileSessionStore>.Instance);
}
