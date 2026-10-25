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
/// With a topology, the worker first wires its peer channels (between steps 2 and 3):
///   a. Open one endpoint per neighbour with a higher index; send them as WorkerEndpoints.
///   b. Read WorkerDialPlan and dial every neighbour with a lower index; each new channel
///      starts with the dialer's index, which the accepting side checks.
///   c. After step 6, wait for WorkerRelease before closing the peer channels, so no channel
///      is reset while a neighbour still reads from it.
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

        var ownFile = layerInput.PerWorkerFiles
            ? JsonSerializer.Deserialize<WorkerInputFileDto>(ReadInputFile(moduleInfo, $"worker_{index}.json"))
            : null;
        var workerInputs = layerInput.WorkerInputs;
        var workerInput = ownFile is not null
            ? ownFile.WorkerInput
            : workerInputs is not null && index < workerInputs.Count ? workerInputs[index] : null;
        IReadOnlyList<string?> inheritedOutputs = ownFile?.InheritedOutputs
            ?? [.. layerInput.PreviousLayerResults.Select(r => LayerResultDocument.ReadOutputs(r) is var o && index < o.Count ? o[index] : null)];

        var spec = AgentTopologySpec.Parse(layerInput.Topology, layerInput.TopologyEdges).Validate(layerInput.TotalWorkers);
        var channelTimeout = TimeSpan.FromSeconds(Math.Max(1, layerInput.ChannelTimeoutSeconds));

        moduleInfo.Logger.LogInformation(
            "Worker {Index}/{Total} starting — session={Session} layer={Layer} topology={Topology}",
            index, layerInput.TotalWorkers, layerInput.SessionId, layerInput.LayerId, spec);

        var peers = new Dictionary<int, PeerChannel>();
        string? wiringError = null;
        if (!spec.IsNone)
        {
            try
            {
                peers = await WirePeersAsync(moduleInfo, spec, index, layerInput.TotalWorkers, channelTimeout, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                wiringError = $"Wiring the {spec} channels failed: {ex.Message}";
            }
        }

        var input = new AgentLayerInput
        {
            WorkerIndex             = index,
            TotalWorkers            = layerInput.TotalWorkers,
            SessionId               = layerInput.SessionId,
            LayerId                 = layerInput.LayerId,
            PreviousLayerResultJson = layerInput.PreviousLayerResults.FirstOrDefault(),
            PreviousLayerResults    = layerInput.PreviousLayerResults,
            CustomData              = layerInput.CustomData,
            WorkerInput             = workerInput,
            Parameters              = layerInput.Parameters,
            InheritedOutputs        = inheritedOutputs,
            Topology                = new AgentTopology(spec, index, layerInput.TotalWorkers,
                                          peers.ToDictionary(p => p.Key, p => (IAgentChannel)p.Value)),
        };

        WorkerResult result;
        try
        {
            if (wiringError is not null)
                throw new InvalidOperationException(wiringError);

            // --- Steps 3–5: load assembly and execute ---
            var outputData = await ExecuteUserCodeAsync(assemblyBytes, layerInput.EntryPoint, input, cancellationToken);
            await Task.WhenAll(peers.Values.Select(p => p.FlushAsync()));

            stopwatch.Stop();
            result = new WorkerResult
            {
                WorkerIndex    = input.WorkerIndex,
                Success        = true,
                OutputData     = outputData,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
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
            };

            // Close at once, so neighbours waiting on this worker fail now instead of timing out.
            await DisposePeersAsync(peers);
        }

        // --- Step 6: return result ---
        await moduleInfo.Parent.WriteObjectAsync(result);

        if (!spec.IsNone)
        {
            await moduleInfo.Parent.ReadObjectAsync<WorkerRelease>();
            await DisposePeersAsync(peers);
        }
    }

    /// <summary>
    /// Connects this worker to its neighbours: listens for the higher-indexed ones, dials the
    /// lower-indexed ones through the main module's dial plan. Every channel opens with the
    /// dialer's index, so a mis-wired edge fails loudly instead of mixing up neighbours.
    /// </summary>
    private static async Task<Dictionary<int, PeerChannel>> WirePeersAsync(
        IModuleInfo moduleInfo, AgentTopologySpec spec, int index, int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var neighbors = spec.NeighborsOf(index, count);
        var endpoints = new Dictionary<int, IPeerEndpoint>();
        var announcement = new WorkerEndpoints { WorkerIndex = index };
        try
        {
            foreach (var peer in neighbors.Where(p => p > index))
            {
                var endpoint = await moduleInfo.OpenEndpointAsync();
                endpoints[peer] = endpoint;
                announcement.Endpoints[peer] = endpoint.Address;
            }
        }
        catch (Exception ex)
        {
            announcement.Error = ex.Message;
        }

        // Always answer, so the main module learns of the failure instead of waiting.
        await moduleInfo.Parent.WriteObjectAsync(announcement);
        if (announcement.Error is not null)
            throw new InvalidOperationException(announcement.Error);

        var plan = await moduleInfo.Parent.ReadObjectAsync<WorkerDialPlan>();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var dialed = plan.Dial.Select(async entry =>
        {
            var channel = await moduleInfo.ConnectAsync(entry.Value).WaitAsync(timeoutSource.Token);
            await channel.WriteDataAsync(index);
            return (Peer: entry.Key, Channel: channel);
        });
        var accepted = endpoints.Select(async entry =>
        {
            var channel = await entry.Value.AcceptAsync(timeoutSource.Token);
            var hello = await channel.ReadIntAsync().WaitAsync(timeoutSource.Token);
            if (hello != entry.Key)
                throw new InvalidOperationException($"Expected worker {entry.Key} on this endpoint, but worker {hello} connected.");
            return (Peer: entry.Key, Channel: channel);
        });

        try
        {
            var channels = await Task.WhenAll(dialed.Concat(accepted));
            return channels.ToDictionary(c => c.Peer, c => new PeerChannel(c.Peer, c.Channel, timeout));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Not every neighbour of worker {index} connected within {timeout.TotalSeconds:0.#}s.");
        }
        finally
        {
            foreach (var endpoint in endpoints.Values)
            {
                await endpoint.DisposeAsync();
            }
        }
    }

    private static async Task DisposePeersAsync(Dictionary<int, PeerChannel> peers)
    {
        foreach (var peer in peers.Values)
        {
            await peer.DisposeAsync();
        }
        peers.Clear();
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
