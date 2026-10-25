using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Runtime;
using Parcs.Modules.AgentRunner.Models;
using Parcs.Net;

namespace Parcs.Modules.AgentRunner;

/// <summary>
/// Lets a running worker spawn points one level deeper (the R of PARCS): new points run the
/// same session's code, and their results go back to the worker that spawned them.
///
/// Budget: the layer's maxPoints is split among its workers, and each spawn hands the rest of
/// the spawning worker's share down to the new children, so every subtree has a fixed budget and
/// no central counter is needed. All the layer's points were admitted when it started, so a
/// parent never waits for children that cannot be scheduled.
///
/// A spawn is a small control space of its own: the spawning worker (family index 0) and its
/// children (1..n) exchange endpoint addresses over the control channels, like the main module
/// and its workers, and then open peer channels on demand. Children see their siblings as
/// input.Peer(0..n-1) and the spawning worker as input.Spawner.
/// </summary>
internal sealed class WorkerSpawner(
    IModuleInfo moduleInfo, int depth, int maxDepth, int budget, string? entryPoint, TimeSpan channelTimeout) : IAgentSpawner
{
    private readonly Lock _lock = new();
    private readonly List<Spawn> _spawns = [];
    private int _remaining = budget;

    public int Remaining { get { lock (_lock) return _remaining; } }

    public async Task<IAgentSpawn> SpawnAsync(
        int count, string? childEntryPoint, IReadOnlyList<string?>? inputs, int? budgetPerChild, CancellationToken cancellationToken)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Spawn at least one point.");
        if (inputs is not null && inputs.Count != count)
            throw new ArgumentException($"inputs has {inputs.Count} entries; it needs one per spawned point ({count}).", nameof(inputs));
        if (depth >= maxDepth)
        {
            throw new InvalidOperationException(maxDepth == 0
                ? "spawn_limit: spawning is off for this layer; run it with maxPoints above parallelism."
                : $"spawn_limit: this worker is at depth {depth}, the layer's maxDepth; run the layer with a larger maxDepth to spawn deeper.");
        }

        int[] childBudgets;
        lock (_lock)
        {
            if (count > _remaining)
            {
                throw new InvalidOperationException(
                    $"spawn_limit: spawning {count} points needs {count} of this worker's budget, which has {_remaining} left " +
                    "(the layer's maxPoints is split among its workers and handed down the spawn tree).");
            }

            var left = _remaining - count;
            if (budgetPerChild is int each)
            {
                if (each < 0 || (long)each * count > left)
                    throw new InvalidOperationException($"spawn_limit: {count} children with {each} each need {(long)each * count}, but {left} is left after spawning them.");
                childBudgets = [.. Enumerable.Repeat(each, count)];
            }
            else
            {
                childBudgets = [.. Enumerable.Range(0, count).Select(i => left / count + (i < left % count ? 1 : 0))];
            }

            _remaining = left - childBudgets.Sum();
        }

        var spawn = await StartAsync(count, childEntryPoint ?? entryPoint, inputs, childBudgets, cancellationToken);
        lock (_lock)
        {
            _spawns.Add(spawn);
        }

        moduleInfo.Logger.LogInformation(
            "Spawned {Count} points at depth {Depth} (budgets {Budgets})", count, depth + 1, string.Join(",", childBudgets));
        return spawn;
    }

    /// <summary>
    /// After the worker's code returns: waits for every child, releases it and frees its point.
    /// Returns how many points were spawned below this worker, at every depth.
    /// </summary>
    public async Task<int> CompleteAsync()
    {
        var total = 0;
        foreach (var spawn in Snapshot())
            total += await spawn.CompleteAsync();
        return total;
    }

    /// <summary>When the worker failed: drops the children at once.</summary>
    public async Task AbortAsync()
    {
        foreach (var spawn in Snapshot())
            await spawn.AbortAsync();
    }

    private List<Spawn> Snapshot()
    {
        lock (_lock)
        {
            return [.. _spawns];
        }
    }

    private async Task<Spawn> StartAsync(
        int count, string? childEntryPoint, IReadOnlyList<string?>? inputs, int[] childBudgets, CancellationToken cancellationToken)
    {
        var endpoint = await moduleInfo.OpenEndpointAsync();
        IPoint[] points = [];
        try
        {
            points = await moduleInfo.CreatePointsAsync(count);
            var control = new IChannel[count];
            for (var i = 0; i < count; i++)
                control[i] = await points[i].CreateChannelAsync();
            foreach (var point in points)
                await point.ExecuteClassAsync<AgentRunnerWorkerModule>();

            for (var i = 0; i < count; i++)
            {
                await control[i].WriteObjectAsync(new WorkerHandshake
                {
                    WorkerIndex = i,
                    Depth       = depth + 1,
                    MaxDepth    = maxDepth,
                    SpawnBudget = childBudgets[i],
                    Siblings    = count,
                    EntryPoint  = childEntryPoint,
                    WorkerInput = inputs?[i],
                });
            }

            // Same exchange as the main module's: every child's endpoint, then the family directory.
            var addresses = await Task.WhenAll(control.Select(async c =>
            {
                try { return (await c.ReadObjectAsync<WorkerEndpoint>()).Address; }
                catch { return null; }
            })).WaitAsync(cancellationToken);
            var directory = new WorkerDirectory { Addresses = [endpoint.Address, .. addresses] };
            foreach (var c in control)
                await c.WriteObjectAsync(directory);

            var family = new PeerMesh(0, directory.Addresses, endpoint, moduleInfo.ConnectAsync, channelTimeout);
            return new Spawn(depth + 1, points, control, family);
        }
        catch
        {
            await endpoint.DisposeAsync();
            foreach (var point in points)
            {
                try { await point.DeleteAsync(); } catch { /* best-effort */ }
            }
            throw;
        }
    }
}

/// <summary>Children of one <see cref="IAgentSpawner.SpawnAsync"/> call.</summary>
internal sealed class Spawn(int depth, IPoint[] points, IChannel[] control, PeerMesh family) : IAgentSpawn
{
    private readonly ConcurrentDictionary<int, IAgentChannel> _children = new();
    private readonly Task<WorkerResult>?[] _childResults = new Task<WorkerResult>?[points.Length];
    private Task<WorkerResult[]>? _allResults;

    public int Count => points.Length;

    public IAgentChannel Child(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"This spawn has children 0..{Count - 1}.");
        return _children.GetOrAdd(index, i => new RenumberedChannel(family[i + 1], i));
    }

    public async Task<IReadOnlyList<string?>> ResultsAsync(CancellationToken cancellationToken = default)
    {
        var results = await AllResults().WaitAsync(cancellationToken);
        if (results.FirstOrDefault(r => !r.Success) is { } failed)
            throw new InvalidOperationException($"Spawned worker {failed.WorkerIndex} at depth {depth} failed: {failed.ErrorMessage}");
        return [.. results.Select(r => r.OutputData)];
    }

    /// <summary>Waits for the children, releases them and frees their points; returns the points spawned here and below.</summary>
    public async Task<int> CompleteAsync()
    {
        var results = await AllResults();
        await family.FlushAsync();
        await family.DisposeAsync();
        for (var i = 0; i < Count; i++)
            await ReleaseAsync(i);
        return Count + results.Sum(r => r.Spawned);
    }

    /// <summary>
    /// When the spawning worker failed: children that reported are released as usual; the
    /// control channels of those still running are closed, and they stop once they notice.
    /// Deleting a point whose module still reads its parent channel would hand it a control
    /// signal instead of data.
    /// </summary>
    public async Task AbortAsync()
    {
        await family.DisposeAsync();
        for (var i = 0; i < Count; i++)
        {
            if (_childResults[i] is { IsCompletedSuccessfully: true })
                await ReleaseAsync(i);
            else
                control[i].Dispose();
        }
    }

    private async Task ReleaseAsync(int child)
    {
        try { await control[child].WriteObjectAsync(new WorkerRelease()); } catch { /* the child is gone */ }
        try { await points[child].DeleteAsync(); } catch { /* best-effort */ }
    }

    // Starts reading every child's result once; reading is what the children wait for before they finish.
    private Task<WorkerResult[]> AllResults()
    {
        lock (_childResults)
        {
            return _allResults ??= Task.WhenAll(Enumerable.Range(0, Count).Select(ResultOf));
        }
    }

    private Task<WorkerResult> ResultOf(int child)
    {
        lock (_childResults)
        {
            return _childResults[child] ??= ReadResultAsync(control[child], child);
        }
    }

    private static async Task<WorkerResult> ReadResultAsync(IChannel channel, int child)
    {
        try
        {
            return await channel.ReadObjectAsync<WorkerResult>();
        }
        catch (Exception ex)
        {
            return new WorkerResult { WorkerIndex = child, Success = false, ErrorMessage = $"Communication error: {ex.Message}" };
        }
    }
}

/// <summary>
/// A spawned worker's view of its family mesh: siblings are 0..n-1 (family 1..n), and the
/// spawning worker (family 0) is reached through input.Spawner instead.
/// </summary>
internal sealed class FamilyPeers(PeerMesh family, int index, int siblings) : IAgentPeers
{
    private readonly ConcurrentDictionary<int, IAgentChannel> _channels = new();

    public IAgentChannel this[int worker]
    {
        get
        {
            Check(worker);
            return _channels.GetOrAdd(worker, w => new RenumberedChannel(family[w + 1], w));
        }
    }

    public IReadOnlyList<int> Connected => [.. family.Connected.Where(p => p > 0).Select(p => p - 1)];

    public Task<IAgentChannel> AcceptAsync(CancellationToken cancellationToken = default) =>
        AcceptAsync(Timeout.InfiniteTimeSpan, cancellationToken, useDefault: true);

    public Task<IAgentChannel> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        AcceptAsync(timeout, cancellationToken, useDefault: false);

    public Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, CancellationToken cancellationToken = default) =>
        ReceiveAnyAsync(workers, null, cancellationToken);

    public Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        ReceiveAnyAsync(workers, (TimeSpan?)timeout, cancellationToken);

    private async Task<(int Peer, string Message)> ReceiveAnyAsync(IEnumerable<int> workers, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var targets = workers.Select(w => { Check(w); return w + 1; }).ToList();
        var (peer, message) = timeout is { } t
            ? await family.ReceiveAnyAsync(targets, t, cancellationToken)
            : await family.ReceiveAnyAsync(targets, cancellationToken);
        return (peer - 1, message);
    }

    private async Task<IAgentChannel> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken, bool useDefault)
    {
        while (true)
        {
            var channel = useDefault ? await family.AcceptAsync(cancellationToken) : await family.AcceptAsync(timeout, cancellationToken);
            if (channel.Peer > 0)
                return _channels.GetOrAdd(channel.Peer - 1, w => new RenumberedChannel(channel, w));
            // The spawning worker is input.Spawner, not a sibling.
        }
    }

    private void Check(int worker)
    {
        if (worker < 0 || worker >= siblings)
            throw new ArgumentOutOfRangeException(nameof(worker), worker, $"This worker has siblings 0..{siblings - 1}.");
        if (worker == index)
            throw new ArgumentException($"Worker {worker} cannot open a channel to itself.", nameof(worker));
    }
}

/// <summary>A channel shown under another worker number; everything else goes to the inner channel.</summary>
internal sealed class RenumberedChannel(IAgentChannel inner, int peer) : IAgentChannel
{
    public int Peer => peer;

    public Task SendAsync(string message, CancellationToken cancellationToken = default) => inner.SendAsync(message, cancellationToken);

    public Task<string> ReceiveAsync(CancellationToken cancellationToken = default) => inner.ReceiveAsync(cancellationToken);

    public Task<string> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => inner.ReceiveAsync(timeout, cancellationToken);

    public bool HasMessage => inner.HasMessage;

    public bool TryReceive(out string message) => inner.TryReceive(out message);
}
