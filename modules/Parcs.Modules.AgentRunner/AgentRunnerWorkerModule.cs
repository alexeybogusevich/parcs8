using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
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
///   4. Find the single type implementing IAgentComputation.
///   5. Execute IAgentComputation.ExecuteAsync.
///   6. Write WorkerResult back to the parent channel.
/// </summary>
public sealed class AgentRunnerWorkerModule : IModule
{
    public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // --- Step 1: receive assembly bytes ---
        var assemblyBytes = await moduleInfo.Parent.ReadBytesAsync();

        // --- Step 2: receive worker input ---
        var input = await moduleInfo.Parent.ReadObjectAsync<AgentLayerInput>();

        moduleInfo.Logger.LogInformation(
            "Worker {Index}/{Total} starting — session={Session} layer={Layer}",
            input.WorkerIndex, input.TotalWorkers, input.SessionId, input.LayerId);

        WorkerResult result;
        try
        {
            // --- Steps 3–5: load assembly and execute ---
            var outputData = await ExecuteUserCodeAsync(assemblyBytes, input, cancellationToken);

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
        }

        // --- Step 6: return result ---
        await moduleInfo.Parent.WriteObjectAsync(result);
    }

    private static async Task<string?> ExecuteUserCodeAsync(
        byte[] assemblyBytes,
        AgentLayerInput input,
        CancellationToken cancellationToken)
    {
        // Create an isolated context that shares Parcs.Agent.Runtime with THIS context.
        // Sharing ensures the IAgentComputation interface has the same type identity on both sides
        // of the cast, avoiding an InvalidCastException when the loaded type is assigned to it.
        var context = new AgentAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
        var userAssembly = context.LoadFromStream(new MemoryStream(assemblyBytes));

        // Find the IAgentComputation implementation
        var computationType = userAssembly.GetTypes()
            .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface
                              && t.GetInterfaces().Any(i => i.FullName == typeof(IAgentComputation).FullName))
            ?? throw new InvalidOperationException(
                "No concrete type implementing IAgentComputation was found in the submitted assembly.");

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
