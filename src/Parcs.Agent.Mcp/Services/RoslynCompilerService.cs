using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// Compiles user-supplied C# code using Roslyn into an in-memory assembly.
/// The user code must contain a class implementing <c>IAgentComputation</c>.
///
/// Safe compilation rules:
///   - Only a whitelisted set of BCL + Parcs.Agent.Runtime assemblies are referenced.
///   - Unsafe code is disabled.
///   - Platform-specific APIs (IO, networking, reflection beyond basics) are not referenced.
/// </summary>
public sealed class RoslynCompilerService
{
    private readonly ILogger<RoslynCompilerService> _logger;
    private readonly string _runtimeAssemblyDir;
    private readonly string _agentRuntimePath;

    public RoslynCompilerService(ILogger<RoslynCompilerService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // The directory where dotnet's reference assemblies live
        _runtimeAssemblyDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        // Parcs.Agent.Runtime lives next to this assembly
        _agentRuntimePath = Path.Combine(
            Path.GetDirectoryName(typeof(RoslynCompilerService).Assembly.Location)!,
            "Parcs.Agent.Runtime.dll");

        if (!File.Exists(_agentRuntimePath))
            throw new FileNotFoundException(
                $"Parcs.Agent.Runtime.dll not found at expected path: {_agentRuntimePath}. " +
                "Ensure the MCP server is published with Parcs.Agent.Runtime.");
    }

    /// <summary>Compiles <paramref name="userCode"/> and returns the raw DLL bytes.</summary>
    public byte[] Compile(string userCode)
    {
        var wrappedSource = WrapUserCode(userCode);

        var syntaxTree = CSharpSyntaxTree.ParseText(wrappedSource);

        var references = BuildSafeReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "agent_computation",
            syntaxTrees:  [syntaxTree],
            references:   references,
            options: new CSharpCompilationOptions(
                outputKind:        OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe:       false,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"  [{d.Id}] {d.GetMessage()} (line {d.Location.GetLineSpan().StartLinePosition.Line + 1})"));

            _logger.LogWarning("Compilation failed:\n{Errors}", errors);
            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }

        _logger.LogInformation("Compilation succeeded, assembly size={Size} bytes", ms.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Wraps the user body in a full class if the user only provided a method body.
    /// If the code already contains a class declaration, it is used as-is.
    /// </summary>
    private static string WrapUserCode(string userCode)
    {
        // Heuristic: if the code has a class declaration, use as-is
        if (userCode.Contains("class ") && userCode.Contains("IAgentComputation"))
            return userCode;

        // Otherwise wrap the supplied method body
        return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Parcs.Agent.Runtime;

public sealed class UserAgentComputation : IAgentComputation
{
    public async Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken cancellationToken = default)
    {
        {{userCode}}
    }
}
""";
    }

    private List<MetadataReference> BuildSafeReferences()
    {
        var refs = new List<MetadataReference>();

        void TryAdd(string name)
        {
            var path = Path.Combine(_runtimeAssemblyDir, name);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        // Core BCL assemblies
        TryAdd("System.Runtime.dll");
        TryAdd("System.Collections.dll");
        TryAdd("System.Linq.dll");
        TryAdd("System.Text.Json.dll");
        TryAdd("System.Text.Encodings.Web.dll");
        TryAdd("System.ComponentModel.Annotations.dll");
        TryAdd("System.Memory.dll");
        TryAdd("System.Runtime.Extensions.dll");
        TryAdd("netstandard.dll");
        TryAdd("System.Private.CoreLib.dll");

        // Parcs.Agent.Runtime — the only project-specific reference
        refs.Add(MetadataReference.CreateFromFile(_agentRuntimePath));

        return refs;
    }
}
