using System.Text.Json;
using OneLoaf.Assistant.Application.Claude;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Finds a tool by name.</summary>
public sealed class ToolRegistry(IEnumerable<IAssistantTool> tools)
{
    private readonly Dictionary<string, IAssistantTool> _tools = tools.ToDictionary(t => t.Definition.Name, StringComparer.Ordinal);

    /// <summary>Definitions in a stable (alphabetical) order, so the cached prompt prefix does not change.</summary>
    public IReadOnlyList<ClaudeToolDefinition> Definitions =>
        _tools.Values.Select(t => t.Definition).OrderBy(d => d.Name, StringComparer.Ordinal).ToList();

    /// <summary>Only the named tools.</summary>
    public IReadOnlyList<ClaudeToolDefinition> DefinitionsFor(params string[] names) =>
        Definitions.Where(d => names.Contains(d.Name, StringComparer.Ordinal)).ToList();

    /// <summary>Executes a tool; an unknown tool or a failure becomes an error result for the model.</summary>
    public async Task<ToolResult> ExecuteAsync(string name, JsonElement input, ToolContext context, CancellationToken cancellationToken) =>
        _tools.TryGetValue(name, out var tool)
            ? await tool.ExecuteAsync(input, context, cancellationToken)
            : ToolResult.Error($"Unknown tool '{name}'.");
}
