using System.Text.Json;
using Flurl.Http;
using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Services;
using Parcs.Agent.Mcp.Tools;

namespace Parcs.Agent.Mcp.Tests;

public class SessionManagerTests
{
    // ─── Sessions ────────────────────────────────────────────────────

    [Test]
    public void SessionListsEntryPointsAndSameSourceIsReused()
    {
        var harness = new Harness();

        // Normalise first: the literal's line endings follow the checkout (core.autocrlf).
        var source = Harness.MapReduceSource.Replace("\r\n", "\n");
        var (first, firstReused) = harness.Sessions.CreateSession(source);
        var (second, secondReused) = harness.Sessions.CreateSession(source.Replace("\n", "\r\n") + "\n\n");

        Assert.Multiple(() =>
        {
            Assert.That(first.EntryPoints, Is.EqualTo(new[] { "Demo.Map", "Demo.Reduce" }));
            Assert.That(firstReused, Is.False);
            Assert.That(secondReused, Is.True);
            Assert.That(second.SessionId, Is.EqualTo(first.SessionId));
        });
    }

    [Test]
    public void CompileErrorsReportLinesOfTheSubmittedBody()
    {
        var harness = new Harness();

        var ex = Assert.Throws<CompilationFailedException>(() =>
            harness.Sessions.CreateSession("var x = 1;\nreturn AgentLayerResult.Ok(missing);"));

        Assert.That(ex!.Diagnostics, Has.Some.Matches<CompileDiagnostic>(d => d.Id == "CS0103" && d.Line == 2));
    }

    [Test]
    public void SourceWithoutAComputationIsACompileError()
    {
        var harness = new Harness();

        var ex = Assert.Throws<CompilationFailedException>(() =>
            harness.Sessions.CreateSession("public class NotAComputation { } // IAgentComputation"));

        Assert.That(ex!.Diagnostics.Single().Id, Is.EqualTo("PARCS001"));
    }

    // ─── Running and waiting ─────────────────────────────────────────

    [Test]
    public async Task ReducerOutputIsReturnedParsed()
    {
        var harness = new Harness();
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 1 });

        var envelope = LayerResponses.Build(layer, null);

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Ok, Is.True);
            Assert.That(envelope.Status, Is.EqualTo("Completed"));
            Assert.That(envelope.Output!["value"]!.GetValue<int>(), Is.EqualTo(42));
            Assert.That(envelope.Layer!.SuccessCount, Is.EqualTo(1));
            Assert.That(envelope.Outputs, Is.Null, "summary view has no per-worker outputs");
        });
    }

    [Test]
    public async Task BoundedWaitReturnsRunningAndTheLayerFinishesLater()
    {
        var harness = new Harness();
        harness.Api.HoldRuns = true;
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 1 }, waitForCompletion: false);

        await harness.Sessions.WaitAsync(layer, TimeSpan.FromMilliseconds(100));
        Assert.That(LayerResponses.Build(layer, null), Has.Property(nameof(ToolEnvelope.Status)).EqualTo("Running")
                                                         .And.Property(nameof(ToolEnvelope.Ok)).True);

        await harness.Api.ReleaseAsync(1);
        await harness.Sessions.WaitAsync(layer, TimeSpan.FromSeconds(5));
        Assert.That(layer.Status, Is.EqualTo(LayerStatus.Completed));
    }

    [Test]
    public async Task RepeatedLayerKeyReturnsTheSameLayerWithoutASecondJob()
    {
        var harness = new Harness();
        var request = new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2, LayerKey = "map-0" };

        var first = await harness.RunAsync(request);
        var again = await harness.Sessions.StartLayerAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(again.Reused, Is.True);
            Assert.That(again.Layer!.LayerId, Is.EqualTo(first.LayerId));
            Assert.That(harness.Api.JobsCreated, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LayerKeyWithDifferentArgumentsIsAConflict()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession();
        await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, LayerKey = "map-0" });

        var conflict = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 3, LayerKey = "map-0" });

        Assert.That(conflict.Error!.Code, Is.EqualTo(ErrorCodes.LayerKeyConflict));
    }

    [Test]
    public async Task FailedLayerKeyStartsANewAttempt()
    {
        var harness = new Harness();
        var request = new LayerRequest { SessionId = harness.NewSession(), Parallelism = 1, LayerKey = "k" };
        harness.Api.RunFailure = new InvalidOperationException(
            "Job 1 Failed: Timed out after 120s waiting for daemons to connect for job 1: 0/1 connected.");

        var failed = await harness.RunAsync(request);
        harness.Api.RunFailure = null;
        var retried = await harness.RunAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(failed.Status, Is.EqualTo(LayerStatus.Failed));
            Assert.That(failed.Error!.Code, Is.EqualTo(ErrorCodes.DaemonTimeout));
            Assert.That(failed.Error.Retryable, Is.True);
            Assert.That(retried.LayerId, Is.Not.EqualTo(failed.LayerId));
            Assert.That(retried.Status, Is.EqualTo(LayerStatus.Completed));
        });
    }

    // ─── Validation ──────────────────────────────────────────────────

    [Test]
    public async Task ParallelismAboveCapacityIsRejected()
    {
        var harness = new Harness(maxParallelism: 4);

        var result = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 5 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Error!.Code, Is.EqualTo(ErrorCodes.CapacityExceeded));
            Assert.That(harness.Api.JobsCreated, Is.Zero);
        });
    }

    [Test]
    public async Task SeveralEntryPointsRequireAChoice()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession(Harness.MapReduceSource);

        var missing = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1 });
        var bySimpleName = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, EntryPoint = "Reduce" });

        Assert.Multiple(() =>
        {
            Assert.That(missing.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(bySimpleName.EntryPoint, Is.EqualTo("Demo.Reduce"));
        });
    }

    [Test]
    public async Task WorkerInputsNeedOneEntryPerWorker()
    {
        var harness = new Harness();

        var result = await harness.Sessions.StartLayerAsync(new LayerRequest
        {
            SessionId = harness.NewSession(), Parallelism = 3, WorkerInputs = ["a", "b"],
        });

        Assert.That(result.Error!.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
    }

    [Test]
    public async Task ChildLayerReceivesEveryParentAndItsInputs()
    {
        var harness = new Harness();
        var sessionId = harness.NewSession(Harness.MapReduceSource);
        var left = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, EntryPoint = "Map" });
        var right = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2, EntryPoint = "Map", Parameters = new Dictionary<string, string> { ["side"] = "r" } });

        var child = await harness.RunAsync(new LayerRequest
        {
            SessionId        = sessionId,
            Parallelism      = 2,
            EntryPoint       = "Demo.Reduce",
            PreviousLayerIds = [left.LayerId, right.LayerId],
            WorkerInputs     = ["w0", "w1"],
            FailFast         = true,
        });

        using var input = JsonDocument.Parse(harness.Api.LayerInputs[child.JobId!.Value]);
        var root = input.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("EntryPoint").GetString(), Is.EqualTo("Demo.Reduce"));
            Assert.That(root.GetProperty("PreviousLayerResults").GetArrayLength(), Is.EqualTo(2));
            Assert.That(root.GetProperty("WorkerInputs")[1].GetString(), Is.EqualTo("w1"));
            Assert.That(root.GetProperty("FailFast").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("TotalWorkers").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RunningParentIsNotReadyYet()
    {
        var harness = new Harness();
        harness.Api.HoldRuns = true;
        var sessionId = harness.NewSession();
        var parent = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1 }, waitForCompletion: false);

        var child = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, PreviousLayerIds = [parent.LayerId] });

        Assert.Multiple(() =>
        {
            Assert.That(child.Error!.Code, Is.EqualTo(ErrorCodes.PreviousLayerNotReady));
            Assert.That(child.Error.Retryable, Is.True);
        });
    }

    [Test]
    public async Task ModuleNotRegisteredIsRetryable()
    {
        var harness = new Harness();
        harness.Module.IsReady = false;

        var result = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 1 });

        Assert.That(result.Error, Has.Property(nameof(ToolError.Code)).EqualTo(ErrorCodes.NotReady)
                                     .And.Property(nameof(ToolError.Retryable)).True);
    }

    // ─── Partial failure ─────────────────────────────────────────────

    [Test]
    public async Task FailedWorkersMakeTheLayerCompletedWithFailures()
    {
        var harness = new Harness();
        harness.Api.Output = _ => FakeParcsApi.Results((true, "1"), (false, "boom"), (true, "3"));
        var sessionId = harness.NewSession();

        var layer = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 3 });
        var envelope = LayerResponses.Build(layer, "outputs");
        var child = await harness.Sessions.StartLayerAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1, PreviousLayerIds = [layer.LayerId] });

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Ok, Is.False);
            Assert.That(envelope.Status, Is.EqualTo("CompletedWithFailures"));
            Assert.That(envelope.Error!.Code, Is.EqualTo(ErrorCodes.WorkerError));
            Assert.That(envelope.Error.FailedWorkers, Is.EqualTo(new[] { 1 }));
            Assert.That(envelope.Error.Message, Does.Contain("boom"));
            Assert.That(envelope.Outputs!.Select(o => o?.ToJsonString()), Is.EqualTo(new[] { "1", null, "3" }));
            Assert.That(child.Error, Is.Null, "partial results can still be read by the next layer");
        });
    }

    // ─── Queueing and cancellation ───────────────────────────────────

    [Test]
    public async Task LayersThatDoNotFitWaitForFreeWorkers()
    {
        var harness = new Harness(maxParallelism: 3);
        harness.Api.HoldRuns = true;
        var sessionId = harness.NewSession();

        var first = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2 }, waitForCompletion: false);
        await Harness.EventuallyAsync(() => first.Status == LayerStatus.Running, "the first layer runs");
        var second = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2 }, waitForCompletion: false);
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo(LayerStatus.Queued));
            Assert.That(harness.Gate.Queued, Is.EqualTo(1));
        });

        await harness.Api.ReleaseAsync(1);
        await Harness.EventuallyAsync(() => second.Status == LayerStatus.Running, "the second layer is admitted");
        await harness.Api.ReleaseAsync(2);
        await harness.Sessions.WaitAsync(second, TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo(LayerStatus.Completed));
            Assert.That(LayerResponses.Build(second, null).Layer!.QueuedSeconds, Is.GreaterThan(0));
            Assert.That(harness.Gate.InUse, Is.Zero);
        });
    }

    [Test]
    public async Task CancellingARunningLayerCancelsItsJobAndFreesWorkers()
    {
        var harness = new Harness();
        harness.Api.HoldRuns = true;
        var layer = await harness.RunAsync(new LayerRequest { SessionId = harness.NewSession(), Parallelism = 2 }, waitForCompletion: false);
        await Harness.EventuallyAsync(() => harness.Api.Held.ContainsKey(1), "the job runs");

        await harness.Sessions.CancelAsync(layer);

        Assert.Multiple(() =>
        {
            Assert.That(layer.Status, Is.EqualTo(LayerStatus.Cancelled));
            Assert.That(layer.Error!.Code, Is.EqualTo(ErrorCodes.Cancelled));
            Assert.That(harness.Api.CancelledJobs, Does.Contain(1L));
            Assert.That(harness.Gate.InUse, Is.Zero);
        });
    }

    [Test]
    public async Task CancellingAQueuedLayerCreatesNoJob()
    {
        var harness = new Harness(maxParallelism: 2);
        harness.Api.HoldRuns = true;
        var sessionId = harness.NewSession();
        await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 2 }, waitForCompletion: false);
        await Harness.EventuallyAsync(() => harness.Gate.InUse == 2, "the first layer holds the pool");
        var queued = await harness.RunAsync(new LayerRequest { SessionId = sessionId, Parallelism = 1 }, waitForCompletion: false);

        await harness.Sessions.CancelAsync(queued);

        Assert.Multiple(() =>
        {
            Assert.That(queued.Status, Is.EqualTo(LayerStatus.Cancelled));
            Assert.That(queued.JobId, Is.Null);
            Assert.That(harness.Gate.Queued, Is.Zero);
        });
    }

    // ─── Error classification ────────────────────────────────────────

    [TestCase("Job 4 Failed: Worker 3 failed (failFast): Index out of range", ErrorCodes.WorkerError, false)]
    [TestCase("Job 4 Failed: System.OutOfMemoryException", ErrorCodes.OutOfMemory, false)]
    [TestCase("Job 4 Failed: No daemon was resolved. Strategy: KubernetesDaemonResolutionStrategy", ErrorCodes.NoDaemons, true)]
    [TestCase("Job 4 Failed: something else", ErrorCodes.JobFailed, false)]
    public void HostFailuresMapToErrorCodes(string message, string code, bool retryable)
    {
        var error = SessionManager.Classify(new InvalidOperationException(message));

        Assert.That(error, Has.Property(nameof(ToolError.Code)).EqualTo(code)
                              .And.Property(nameof(ToolError.Retryable)).EqualTo(retryable));
    }

    [Test]
    public void FailFastNamesTheFailedWorker()
    {
        var error = SessionManager.Classify(new InvalidOperationException("Job 4 Failed: Worker 3 failed (failFast): x"));

        Assert.That(error.FailedWorkers, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void UnreachableHostIsRetryable()
    {
        var direct = SessionManager.Classify(new HttpRequestException("Connection refused"));
        var wrapped = SessionManager.Classify(
            new InvalidOperationException("POST /api/Jobs failed : ", new HttpRequestException("Connection refused")));

        Assert.Multiple(() =>
        {
            foreach (var error in new[] { direct, wrapped })
            {
                Assert.That(error, Has.Property(nameof(ToolError.Code)).EqualTo(ErrorCodes.HostUnavailable)
                                      .And.Property(nameof(ToolError.Retryable)).True);
            }
        });
    }
}
