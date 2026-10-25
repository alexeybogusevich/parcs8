using System.Reflection;
using ModelContextProtocol.Server;
using Parcs.Agent.Mcp.Models;

namespace Parcs.Agent.Mcp.Tools;

/// <summary>
/// Checks the argument names of a tool call against the tool's parameters before the SDK binds
/// them. A binding failure would otherwise reach the caller as a generic "an error occurred"
/// result, which tells an agent nothing; this returns an invalid_argument envelope that names the
/// missing and unknown arguments, with the likely intended name for a misspelt one.
/// </summary>
public static class ToolArguments
{
    private sealed record Signature(string[] All, string[] Required);

    private static readonly Dictionary<string, Signature> Tools = typeof(ParcsAgentTools)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Select(m => (Method: m, Tool: m.GetCustomAttribute<McpServerToolAttribute>()))
        .Where(t => t.Tool?.Name is not null)
        .ToDictionary(t => t.Tool!.Name!, t =>
        {
            // CancellationToken and other injected parameters are not tool arguments.
            var parameters = t.Method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).ToList();
            return new Signature(
                [.. parameters.Select(p => p.Name!)],
                [.. parameters.Where(p => !p.HasDefaultValue).Select(p => p.Name!)]);
        });

    /// <summary>The error for a call with these argument names, or null when they fit the tool.</summary>
    public static ToolError? Check(string? tool, IEnumerable<string>? argumentNames)
    {
        if (tool is null || !Tools.TryGetValue(tool, out var signature))
            return null;

        var given = (argumentNames ?? []).ToList();
        var missing = signature.Required.Where(r => !given.Contains(r, StringComparer.Ordinal)).ToList();
        var unknown = given.Where(g => !signature.All.Contains(g, StringComparer.Ordinal)).ToList();
        if (missing.Count == 0 && unknown.Count == 0)
            return null;

        var problems = new List<string>();
        if (missing.Count > 0)
            problems.Add($"missing required argument{(missing.Count > 1 ? "s" : "")} {Quote(missing)}");
        foreach (var name in unknown)
        {
            var meant = signature.All.FirstOrDefault(a => Normalize(a) == Normalize(name));
            problems.Add(meant is null ? $"unknown argument '{name}'" : $"unknown argument '{name}' (did you mean '{meant}'?)");
        }

        return new ToolError
        {
            Code      = ErrorCodes.InvalidArgument,
            Message   = $"{tool}: {string.Join("; ", problems)}. Its arguments are {Quote(signature.All)}" +
                        (signature.Required.Length > 0 ? $", of which {Quote(signature.Required)} required." : "."),
            Retryable = false,
        };
    }

    private static string Quote(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));

    // sourceCode, source_code, SourceCode and source-code are the same name to a caller who guessed the casing.
    private static string Normalize(string name) => new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
