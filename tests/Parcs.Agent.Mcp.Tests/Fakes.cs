using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Parcs.Agent.Mcp.Services;

namespace Parcs.Agent.Mcp.Tests;

/// <summary>
/// Stands in for the PARCS Host: jobs complete at once, or are held until released when
/// <see cref="HoldRuns"/> is set, and produce <see cref="Output"/> as agent_results.json.
/// </summary>
internal sealed class FakeParcsApi : IParcsApiClient
{
    private long _nextJobId;

    public bool HoldRuns { get; set; }

    public Exception? RunFailure { get; set; }

    public Func<long, string> Output { get; set; } = _ => Results((true, "{\"value\":42}"));

    public ConcurrentDictionary<long, TaskCompletionSource> Held { get; } = new();

    public ConcurrentDictionary<long, string> LayerInputs { get; } = new();

    public ConcurrentDictionary<long, Dictionary<string, string>> InputFiles { get; } = new();

    public ConcurrentBag<long> CancelledJobs { get; } = new();

    public int JobsCreated => (int)Interlocked.Read(ref _nextJobId);

    public Task<long> CreateJobAsync(
        long moduleId, string assemblyName, string className,
        IEnumerable<(string Filename, byte[] Bytes)> inputFiles,
        IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
    {
        var jobId = Interlocked.Increment(ref _nextJobId);
        LayerInputs[jobId] = Encoding.UTF8.GetString(inputFiles.Single(f => f.Filename == "layer_input.json").Bytes);
        InputFiles[jobId] = inputFiles.Where(f => f.Filename.EndsWith(".json")).ToDictionary(f => f.Filename, f => Encoding.UTF8.GetString(f.Bytes));
        return Task.FromResult(jobId);
    }

    public async Task RunJobAndWaitAsync(long jobId, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken ct = default)
    {
        if (RunFailure is not null)
            throw RunFailure;

        if (HoldRuns)
            await Held.GetOrAdd(jobId, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(ct);
    }

    public Task<byte[]?> GetJobOutputFileAsync(long jobId, string filename, CancellationToken ct = default) =>
        Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(Output(jobId)));

    public Task CancelJobAsync(long jobId, CancellationToken ct = default)
    {
        CancelledJobs.Add(jobId);
        if (Held.TryGetValue(jobId, out var run))
            run.TrySetException(new InvalidOperationException($"Job {jobId} Cancelled: cancelled"));
        return Task.CompletedTask;
    }

    /// <summary>Lets a held job finish; waits until the layer has reached the run call.</summary>
    public async Task ReleaseAsync(long jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!Held.ContainsKey(jobId) && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Held.GetOrAdd(jobId, _ => new()).TrySetResult();
    }

    /// <summary>agent_results.json with one entry per (success, outputData) pair.</summary>
    public static string Results(params (bool Success, string? Output)[] workers) => JsonSerializer.Serialize(new
    {
        sessionId = "s",
        layerId = "l",
        totalElapsedSeconds = 1.0,
        results = workers.Select((w, i) => new
        {
            workerIndex = i,
            success = w.Success,
            outputData = w.Success ? w.Output : null,
            errorMessage = w.Success ? null : w.Output,
            elapsedSeconds = 0.5,
        }),
    });
}

internal sealed class FakeModule : IAgentRunnerModule
{
    public bool IsReady { get; set; } = true;
    public long ModuleId => 1;
    public string AssemblyName => "Parcs.Modules.AgentRunner";
    public string ClassName => "Parcs.Modules.AgentRunner.AgentRunnerMainModule";
}

internal sealed class FakeClusterInfo(int maxParallelism) : IClusterInfoService
{
    public int MaxParallelism { get; set; } = maxParallelism;

    public Task<ClusterInfoResult> GetClusterInfoAsync(CancellationToken ct = default) =>
        Task.FromResult(new ClusterInfoResult
        {
            ScalingMode     = ScalingModes.Static,
            CapacitySource  = CapacitySources.Configuration,
            CurrentCapacity = MaxParallelism,
            MaxParallelism  = MaxParallelism,
        });
}

internal sealed class Harness
{
    public FakeParcsApi Api { get; } = new();
    public FakeModule Module { get; } = new();
    public FakeClusterInfo Cluster { get; }
    public CapacityGate Gate { get; } = new();
    public SessionManager Sessions { get; }

    public Harness(int maxParallelism = 8, ISessionStore? store = null)
    {
        Cluster = new FakeClusterInfo(maxParallelism);
        var compiler = new RoslynCompilerService(
            NullLogger<RoslynCompilerService>.Instance, new ConfigurationBuilder().Build());
        Sessions = new SessionManager(compiler, Api, Module, Cluster, Gate, NullLogger<SessionManager>.Instance, store);
    }

    public const string MapReduceSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using Parcs.Agent.Runtime;

        namespace Demo
        {
            public sealed class Map : IAgentComputation
            {
                public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct = default) =>
                    Task.FromResult(AgentLayerResult.Ok(input.WorkerIndex.ToString()));
            }

            public sealed class Reduce : IAgentComputation
            {
                public Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken ct = default) =>
                    Task.FromResult(AgentLayerResult.Ok("{\"sum\":1}"));
            }
        }
        """;

    public const string BodySource = "return AgentLayerResult.Ok(\"{\\\"value\\\":42}\");";

    public string NewSession(string source = BodySource) => Sessions.CreateSession(source).Session.SessionId;

    public async Task<Models.LayerRecord> RunAsync(LayerRequest request, bool waitForCompletion = true)
    {
        var started = await Sessions.StartLayerAsync(request);
        Assert.That(started.Error, Is.Null, () => started.Error!.Message);
        if (waitForCompletion)
            await Sessions.WaitAsync(started.Layer!, TimeSpan.FromSeconds(10));
        return started.Layer!;
    }

    public static async Task EventuallyAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out waiting until {because}.");
            await Task.Delay(10);
        }
    }
}
