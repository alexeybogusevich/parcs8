using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

namespace Parcs.Agent.Mcp.Tests;

/// <summary>Peer channels, inherit, delete_session and the session store.</summary>
public class LayerOptionsTests
{
    // ─── Peer channels ───────────────────────────────────────────────

    [Test]
    public async Task ChannelTimeoutDefaultsTo60AndFailFastToFalse()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 4 });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("ChannelTimeoutSeconds").GetInt32(), Is.EqualTo(60));
            Assert.That(input.RootElement.GetProperty("FailFast").GetBoolean(), Is.False);
            Assert.That(input.RootElement.TryGetProperty("Topology", out _), Is.False, "the topology is not declared");
        });
    }

    [Test]
    public async Task ExplicitChannelTimeoutReachesTheJob()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 4, FailFast = true, ChannelTimeoutSeconds = 5,
        });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[layer.JobId!.Value]);
        Assert.Multiple(() =>
        {
            Assert.That(input.RootElement.GetProperty("FailFast").GetBoolean(), Is.True);
            Assert.That(input.RootElement.GetProperty("ChannelTimeoutSeconds").GetInt32(), Is.EqualTo(5));
        });
    }

    [TestCase(0)]
    [TestCase(901)]
    public async Task OutOfRangeChannelTimeoutsAreRejected(int channelTimeout)
    {
        var harness = new Harness();
        var started = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 2, ChannelTimeoutSeconds = channelTimeout,
        });

        Assert.Multiple(() =>
        {
            Assert.That(started.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(harness.Api.JobsCreated, Is.Zero);
        });
    }

    [Test]
    public async Task TheGraphTheWorkersBuiltIsReported()
    {
        // A ring of three as the workers report it: each lists the workers it had a channel with.
        var harness = new Harness();
        harness.Api.Output = _ => """
            {"results":[
              {"workerIndex":0,"success":true,"outputData":"0","peers":[1,2]},
              {"workerIndex":1,"success":true,"outputData":"1","peers":[0,2]},
              {"workerIndex":2,"success":true,"outputData":"2","peers":[0,1]}]}
            """;
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 3 });

        var full = LayerResponses.Build(layer, "full");
        Assert.Multiple(() =>
        {
            Assert.That(full.Layer!.PeerChannels, Is.EqualTo(3));
            Assert.That(full.Workers![1].Peers, Is.EqualTo(new[] { 0, 2 }));
        });
    }

    [Test]
    public async Task LayersWithoutPeerChannelsReportNone()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2 });

        var full = LayerResponses.Build(layer, "full");
        Assert.Multiple(() =>
        {
            Assert.That(full.Layer!.PeerChannels, Is.Null);
            Assert.That(full.Workers!.All(w => w.Peers is null), Is.True);
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
            var layer = await before.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, LayerKey = "k", ChannelTimeoutSeconds = 7 });

            var after = new Harness(store: Store(root));
            var restored = after.Sessions.GetLayer(layer.LayerId)!;
            var again = await after.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, LayerKey = "k", ChannelTimeoutSeconds = 7 });
            var child = await after.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, PreviousLayerIds = [layer.LayerId] });

            Assert.Multiple(() =>
            {
                Assert.That(after.Sessions.GetSession(sessionId)!.Assembly, Is.EqualTo(before.Sessions.GetSession(sessionId)!.Assembly));
                Assert.That(after.Sessions.CreateSession(Harness.BodySource).Reused, Is.True);
                Assert.That(restored.Status, Is.EqualTo(LayerStatus.Completed));
                Assert.That(restored.ChannelTimeoutSeconds, Is.EqualTo(7));
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
