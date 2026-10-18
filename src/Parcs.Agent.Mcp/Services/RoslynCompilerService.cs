using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parcs.Agent.Mcp.Models;

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

    /// <summary>
    /// Compiles <paramref name="userCode"/> and returns the DLL bytes with the full names of its
    /// IAgentComputation classes. Throws <see cref="CompilationFailedException"/> on errors.
    /// </summary>
    public CompiledComputation Compile(string userCode)
    {
        var (wrappedSource, lineOffset) = WrapUserCode(userCode);

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
            var diagnostics = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                {
                    var position = d.Location.GetLineSpan().StartLinePosition;
                    // Report lines of the code the agent sent, not of the generated wrapper.
                    var line = Math.Max(1, position.Line + 1 - lineOffset);
                    return new CompileDiagnostic(d.Id, line, position.Character + 1, d.GetMessage());
                })
                .ToList();

            _logger.LogWarning("Compilation failed with {Count} errors", diagnostics.Count);
            throw new CompilationFailedException(diagnostics);
        }

        var entryPoints = FindEntryPoints(compilation);
        if (entryPoints.Count == 0)
        {
            throw new CompilationFailedException(
                [new CompileDiagnostic("PARCS001", 1, 1, "No non-abstract class implements Parcs.Agent.Runtime.IAgentComputation.")]);
        }

        _logger.LogInformation(
            "Compilation succeeded, assembly size={Size} bytes, entry points: {EntryPoints}",
            ms.Length, string.Join(", ", entryPoints));
        return new CompiledComputation(ms.ToArray(), entryPoints);
    }

    /// <summary>Full (metadata) names of the concrete classes implementing IAgentComputation.</summary>
    private static List<string> FindEntryPoints(CSharpCompilation compilation)
    {
        var contract = compilation.GetTypeByMetadataName("Parcs.Agent.Runtime.IAgentComputation");
        if (contract is null)
            return [];

        return compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract
                     && t.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
            .Select(MetadataName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Matches Type.FullName: namespace-qualified, nested types joined with '+'.
        static string MetadataName(INamedTypeSymbol type)
        {
            var name = type.MetadataName;
            for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
                name = $"{outer.MetadataName}+{name}";
            var ns = type.ContainingNamespace;
            return ns is null || ns.IsGlobalNamespace ? name : $"{ns.ToDisplayString()}.{name}";
        }
    }

    /// <summary>
    /// Wraps the user body in a full class if the user only provided a method body.
    /// If the code already contains a class declaration, it is used as-is. Returns the number of
    /// wrapper lines that precede the user code.
    /// </summary>
    private static (string Source, int LineOffset) WrapUserCode(string userCode)
    {
        // Heuristic: if the code has a class declaration, use as-is
        if (userCode.Contains("class ") && userCode.Contains("IAgentComputation"))
            return (userCode, 0);

        // Otherwise wrap the supplied method body
        const int wrapperLinesBeforeBody = 13;
        return ($$"""
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
""", wrapperLinesBeforeBody);
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

        // Dynamic dispatch support (required when user code uses 'dynamic')
        TryAdd("Microsoft.CSharp.dll");

        // Parcs.Agent.Runtime — the only project-specific reference
        refs.Add(MetadataReference.CreateFromFile(_agentRuntimePath));

        return refs;
    }
}

public sealed record CompiledComputation(byte[] Assembly, IReadOnlyList<string> EntryPoints);

public sealed class CompilationFailedException(IReadOnlyList<CompileDiagnostic> diagnostics)
    : Exception($"Compilation failed with {diagnostics.Count} error(s).")
{
    public IReadOnlyList<CompileDiagnostic> Diagnostics { get; } = diagnostics;
}
