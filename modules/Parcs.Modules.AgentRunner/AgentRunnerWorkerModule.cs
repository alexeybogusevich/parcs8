using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Runtime;
using Parcs.Modules.AgentRunner.Models;
using Parcs.Net;

namespace Parcs.Modules.AgentRunner;

/// <summary>
/// Worker (daemon-side) module for agent-driven parallel computation.
///
/// Protocol (mirrors AgentRunnerMainModule):
///   1. Read assembly bytes from parent channel.
///   2. Read AgentLayerInput from parent channel.
///   3. Load the assembly into an isolated AssemblyLoadContext that shares
///      Parcs.Agent.Runtime with the worker's own context (to preserve type identity
///      for the IAgentComputation cast).
///   4. Find the IAgentComputation class named by EntryPoint (or the only one).
///   5. Execute IAgentComputation.ExecuteAsync.
///   6. Write WorkerResult back to the parent channel.
///
/// Peer channels (between steps 2 and 3, and after step 6):
///   a. Open one endpoint and send its address as WorkerEndpoint.
///   b. Read the WorkerDirectory of every worker's endpoint. Nothing is connected yet: the user
///      code opens channels on demand through input.Peer(j), so it builds the topology as it runs.
///   c. After step 6, wait for WorkerRelease before closing the peer channels, so no channel
///      is reset while another worker still reads from it.
///
/// A spawned point runs this same module, started by a worker's SpawnAsync instead of by the
/// main module (see <see cref="WorkerSpawner"/>): its handshake carries its class, input and
/// budget, its peers are its siblings, and its result goes back to the spawning worker.
/// </summary>
public sealed class AgentRunnerWorkerModule : IModule
{
    public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // --- Step 1: receive worker index from main module ---
        var handshake = await moduleInfo.Parent.ReadObjectAsync<WorkerHandshake>();
        var index = handshake.WorkerIndex;

        // --- Step 2: read assembly + layer input from shared NFS storage ---
        var assemblyBytes = ReadInputFile(moduleInfo, "agent_computation.dll");
        var layerInputJson = Encoding.UTF8.GetString(ReadInputFile(moduleInfo, "layer_input.json"));
        var layerInput = JsonSerializer.Deserialize<LayerInputDto>(layerInputJson)
            ?? throw new InvalidOperationException("Failed to deserialise layer_input.json");

        // A spawned point gets its class and input from the spawning worker; a layer worker from the layer files.
        var spawned = handshake.IsSpawned;
        var ownFile = !spawned && layerInput.PerWorkerFiles
            ? JsonSerializer.Deserialize<WorkerInputFileDto>(ReadInputFile(moduleInfo, $"worker_{index}.json"))
            : null;
        var workerInputs = layerInput.WorkerInputs;
        var workerInput = spawned
            ? handshake.WorkerInput
            : ownFile is not null
                ? ownFile.WorkerInput
                : workerInputs is not null && index < workerInputs.Count ? workerInputs[index] : null;
        IReadOnlyList<string?> inheritedOutputs = spawned
            ? []
            : ownFile?.InheritedOutputs
                ?? [.. layerInput.PreviousLayerResults.Select(r => LayerResultDocument.ReadOutputs(r) is var o && index < o.Count ? o[index] : null)];
        var entryPoint = spawned ? handshake.EntryPoint : layerInput.EntryPoint;
        var totalWorkers = handshake.Siblings ?? layerInput.TotalWorkers;

        var channelTimeout = TimeSpan.FromSeconds(Math.Max(1, layerInput.ChannelTimeoutSeconds));

        moduleInfo.Logger.LogInformation(
            "Worker {Index}/{Total} at depth {Depth} starting — session={Session} layer={Layer}",
            index, totalWorkers, handshake.Depth, layerInput.SessionId, layerInput.LayerId);

        // In a spawned point's family mesh, index 0 is the spawning worker and siblings follow it.
        var (peers, peerError) = await OpenPeersAsync(moduleInfo, spawned ? index + 1 : index, channelTimeout);
        IAgentPeers agentPeers = peers is null
            ? new UnavailableAgentPeers($"Worker {index} has no peer channels: {peerError}")
            : spawned ? new FamilyPeers(peers, index, totalWorkers) : peers;
        var spawner = new WorkerSpawner(moduleInfo, handshake.Depth, handshake.MaxDepth, handshake.SpawnBudget, entryPoint, channelTimeout);

        var input = new AgentLayerInput
        {
            WorkerIndex             = index,
            TotalWorkers            = totalWorkers,
            SessionId               = layerInput.SessionId,
            LayerId                 = layerInput.LayerId,
            PreviousLayerResultJson = layerInput.PreviousLayerResults.FirstOrDefault(),
            PreviousLayerResults    = layerInput.PreviousLayerResults,
            CustomData              = layerInput.CustomData,
            WorkerInput             = workerInput,
            Parameters              = layerInput.Parameters,
            InheritedOutputs        = inheritedOutputs,
            Peers                   = agentPeers,
            Depth                   = handshake.Depth,
            MaxDepth                = handshake.MaxDepth,
            // Opened at once, so the spawning worker can talk to this child as soon as it starts.
            Spawner                 = spawned && peers is not null ? new RenumberedChannel(peers[0], -1) : null,
            Spawning                = spawner,
        };

        WorkerResult result;
        try
        {
            // --- Steps 3–5: load assembly and execute ---
            var outputData = await ExecuteUserCodeAsync(assemblyBytes, entryPoint, input, cancellationToken);
            // Children finish before their parent reports, so a subtree is done when its root is.
            var spawnedPoints = await spawner.CompleteAsync();
            if (peers is not null)
                await peers.FlushAsync();

            stopwatch.Stop();
            result = new WorkerResult
            {
                WorkerIndex    = input.WorkerIndex,
                Success        = true,
                OutputData     = outputData,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                Peers          = [.. input.Peers.Connected],
                Spawned        = spawnedPoints,
            };

            moduleInfo.Logger.LogInformation(
                "Worker {Index} succeeded in {Elapsed:F2}s", input.WorkerIndex, result.ElapsedSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            moduleInfo.Logger.LogError(ex,
                "Worker {Index} threw an exception: {Msg}", input.WorkerIndex, ex.Message);

            result = new WorkerResult
            {
                WorkerIndex    = input.WorkerIndex,
                Success        = false,
                ErrorMessage   = ex.Message,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                Peers          = [.. input.Peers.Connected],
            };

            // Close at once, so workers waiting on this one fail now instead of timing out.
            await spawner.AbortAsync();
            if (peers is not null)
            {
                await peers.DisposeAsync();
                peers = null;
            }
        }

        // --- Step 6: return result ---
        try
        {
            await moduleInfo.Parent.WriteObjectAsync(result);
            await moduleInfo.Parent.ReadObjectAsync<WorkerRelease>();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The parent is gone: a spawning worker that failed closes its children's channels.
            // Failing here would report this worker's exit as a failure of the whole job.
            moduleInfo.Logger.LogWarning(
                "Worker {Index} at depth {Depth} lost its parent before it was released: {Msg}", index, handshake.Depth, ex.Message);
        }
        finally
        {
            if (peers is not null)
                await peers.DisposeAsync();
        }
    }

    /// <summary>
    /// Opens this worker's endpoint, announces it and reads everyone's. Always answers the main
    /// module, so a worker without an endpoint still runs; only its peer channels are unavailable.
    /// </summary>
    private static async Task<(PeerMesh? Peers, string? Error)> OpenPeersAsync(IModuleInfo moduleInfo, int index, TimeSpan timeout)
    {
        IPeerEndpoint? endpoint = null;
        var announcement = new WorkerEndpoint { WorkerIndex = index };
        try
        {
            endpoint = await moduleInfo.OpenEndpointAsync();
            announcement.Address = endpoint.Address;
        }
        catch (Exception ex)
        {
            announcement.Error = ex.Message;
        }

        await moduleInfo.Parent.WriteObjectAsync(announcement);
        var directory = await moduleInfo.Parent.ReadObjectAsync<WorkerDirectory>();

        return endpoint is null
            ? (null, announcement.Error)
            : (new PeerMesh(index, directory.Addresses, endpoint, moduleInfo.ConnectAsync, timeout), null);
    }

    private static async Task<string?> ExecuteUserCodeAsync(
        byte[] assemblyBytes,
        string? entryPoint,
        AgentLayerInput input,
        CancellationToken cancellationToken)
    {
        // Create an isolated context that shares Parcs.Agent.Runtime with THIS context.
        // Sharing ensures the IAgentComputation interface has the same type identity on both sides
        // of the cast, avoiding an InvalidCastException when the loaded type is assigned to it.
        var context = new AgentAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
        var userAssembly = context.LoadFromStream(new MemoryStream(assemblyBytes));

        var computationType = FindEntryPoint(userAssembly, entryPoint);

        var instance = Activator.CreateInstance(computationType)
            ?? throw new InvalidOperationException($"Failed to create an instance of {computationType.FullName}.");

        // Invoke through the interface using dynamic dispatch (avoids cast type-identity issues
        // if the context sharing approach is incomplete)
        if (instance is IAgentComputation computation)
        {
            var layerResult = await computation.ExecuteAsync(input, cancellationToken);
            return layerResult.Success ? layerResult.OutputData : throw new Exception(layerResult.ErrorMessage);
        }

        // Fallback: use reflection-based invocation (handles cross-context type identity gap)
        var executeMethod = computationType.GetMethod(nameof(IAgentComputation.ExecuteAsync))
            ?? throw new InvalidOperationException("ExecuteAsync method not found via reflection.");

        var taskObj = executeMethod.Invoke(instance, [input, cancellationToken])
            ?? throw new InvalidOperationException("ExecuteAsync returned null.");

        await (Task)taskObj;

        // Extract .Result via reflection
        var resultProp = taskObj.GetType().GetProperty("Result")
            ?? throw new InvalidOperationException("Task Result property not found.");

        var result = resultProp.GetValue(taskObj)
            ?? throw new InvalidOperationException("ExecuteAsync returned a null result.");

        var successProp  = result.GetType().GetProperty("Success");
        var outputProp   = result.GetType().GetProperty("OutputData");
        var errorProp    = result.GetType().GetProperty("ErrorMessage");

        var success = (bool)(successProp?.GetValue(result) ?? true);
        if (!success)
        {
            var error = (string?)errorProp?.GetValue(result) ?? "Unknown error";
            throw new Exception(error);
        }

        return (string?)outputProp?.GetValue(result);
    }

    /// <summary>
    /// Picks the IAgentComputation class named by <paramref name="entryPoint"/> (full or simple
    /// name), or the only one in the assembly when no name is given.
    /// </summary>
    private static Type FindEntryPoint(Assembly userAssembly, string? entryPoint)
    {
        var candidates = userAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                     && t.GetInterfaces().Any(i => i.FullName == typeof(IAgentComputation).FullName))
            .ToList();

        if (!string.IsNullOrEmpty(entryPoint))
        {
            return candidates.FirstOrDefault(t => t.FullName == entryPoint)
                ?? candidates.SingleOrDefault(t => t.Name == entryPoint)
                ?? throw new InvalidOperationException(
                    $"Entry point '{entryPoint}' was not found among: {string.Join(", ", candidates.Select(t => t.FullName))}.");
        }

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                "No concrete type implementing IAgentComputation was found in the submitted assembly."),
            _ => throw new InvalidOperationException(
                $"The assembly has several IAgentComputation classes; pass entryPoint: {string.Join(", ", candidates.Select(t => t.FullName))}."),
        };
    }

    private static byte[] ReadInputFile(IModuleInfo moduleInfo, string filename)
    {
        using var stream = moduleInfo.InputReader.GetFileStreamForFile(filename);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>
/// Isolated load context for user-submitted assemblies.
/// Passes through <c>Parcs.Agent.Runtime</c> to the parent (default) context so that
/// <see cref="IAgentComputation"/> has the same type identity as the one referenced by
/// the worker module itself.
/// </summary>
internal sealed class AgentAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public AgentAssemblyLoadContext(string hostAssemblyPath)
        : base(name: "AgentUserCode", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(hostAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // For Parcs.Agent.Runtime we need the exact same Assembly object that the worker
        // module itself uses — that keeps IAgentComputation type-identity intact for the cast.
        // The daemon's Default context may NOT have this DLL, so look it up in whichever
        // AssemblyLoadContext the worker module (AgentRunnerWorkerModule) was loaded into.
        if (assemblyName.Name == "Parcs.Agent.Runtime")
        {
            var hostingContext = AssemblyLoadContext.GetLoadContext(
                typeof(AgentRunnerWorkerModule).Assembly);
            return hostingContext?.Assemblies
                .FirstOrDefault(a => a.GetName().Name == "Parcs.Agent.Runtime");
        }

        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolvedPath is not null ? LoadFromAssemblyPath(resolvedPath) : null;
    }
}
