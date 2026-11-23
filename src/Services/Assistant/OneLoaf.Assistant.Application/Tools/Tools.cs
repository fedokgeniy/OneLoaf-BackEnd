using System.Globalization;
using System.Net;
using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Names of the assistant tools (the model sees these names).</summary>
public static class ToolNames
{
    /// <summary>Find recipes by title or flour protein.</summary>
    public const string FindRecipes = "find_recipes";

    /// <summary>Recipe card.</summary>
    public const string GetRecipe = "get_recipe";

    /// <summary>Cost breakdown.</summary>
    public const string CalculateCost = "calculate_cost";

    /// <summary>Timeline and ready window.</summary>
    public const string CalculateTimeline = "calculate_timeline";

    /// <summary>Start window for a ready time.</summary>
    public const string CalculateStartWindow = "calculate_start_window";

    /// <summary>Dough weight, kneading cycles, starter amount.</summary>
    public const string CalculateProductionFigures = "calculate_production_figures";

    /// <summary>Starters with their schedule.</summary>
    public const string ListStarters = "list_starters";

    /// <summary>Plan of a batch without saving.</summary>
    public const string PreviewBatchPlan = "preview_batch_plan";

    /// <summary>Plan of a batch saved as a pending action for the baker to confirm.</summary>
    public const string ProposeBatch = "propose_batch";

    /// <summary>Stage log and feedback of a bake.</summary>
    public const string GetBakeAnalysisData = "get_bake_analysis_data";

    /// <summary>Price history of an ingredient.</summary>
    public const string GetIngredientPrices = "get_ingredient_prices";

    /// <summary>Missing data warnings.</summary>
    public const string GetMissingData = "get_missing_data";
}

/// <summary>A response of the Core API as seen by a tool.</summary>
/// <param name="Status">HTTP status.</param>
/// <param name="Body">Response body (JSON or empty).</param>
public sealed record CoreApiResponse(HttpStatusCode Status, string Body)
{
    /// <summary>2xx.</summary>
    public bool IsSuccess => (int)Status is >= 200 and < 300;
}

/// <summary>
/// The Core API, called on behalf of the current user: implementations forward the user's bearer token and the
/// correlation id, so a tool sees exactly what the baker can see.
/// </summary>
public interface ICoreApiClient
{
    /// <summary>Sends a request; transport failures are mapped to <c>ExternalServiceUnavailableException</c>.</summary>
    Task<CoreApiResponse> SendAsync(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken);
}

/// <summary>State shared by the tools of one turn.</summary>
public sealed class ToolContext(Guid userId, Guid conversationId)
{
    /// <summary>Current user.</summary>
    public Guid UserId { get; } = userId;

    /// <summary>Conversation of the turn.</summary>
    public Guid ConversationId { get; } = conversationId;

    /// <summary>A batch proposal created in this turn, if any.</summary>
    public PendingActionDto? PendingAction { get; set; }
}

/// <summary>Result of a tool call.</summary>
/// <param name="Content">Compact JSON (success) or a short error message.</param>
/// <param name="IsError">The call failed (bad input, not found, Core API error).</param>
public sealed record ToolResult(string Content, bool IsError)
{
    /// <summary>A failed call.</summary>
    public static ToolResult Error(string message) => new(JsonSerializer.Serialize(new { error = message }), true);
}

/// <summary>A tool the model can call. New tools are new classes registered in DI; the registry does not change.</summary>
public interface IAssistantTool
{
    /// <summary>Definition sent to Claude.</summary>
    ClaudeToolDefinition Definition { get; }

    /// <summary>Validates the input and executes the call.</summary>
    Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken);
}

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

/// <summary>Reads and checks tool arguments.</summary>
public static class ToolInput
{
    /// <summary>A required UUID argument.</summary>
    public static bool TryGetGuid(JsonElement input, string name, out Guid value)
    {
        value = Guid.Empty;
        return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            && Guid.TryParse(p.GetString(), out value);
    }

    /// <summary>A required integer argument within a range.</summary>
    public static bool TryGetInt(JsonElement input, string name, int min, int max, out int value)
    {
        value = 0;
        return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            && p.TryGetInt32(out value) && value >= min && value <= max;
    }

    /// <summary>An optional string argument.</summary>
    public static string? GetString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    /// <summary>An optional number argument.</summary>
    public static decimal? GetDecimal(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetDecimal()
            : null;

    /// <summary>A required ISO 8601 instant with an offset.</summary>
    public static bool TryGetInstant(JsonElement input, string name, out DateTimeOffset value)
    {
        value = default;
        var text = GetString(input, name);
        return text is not null && (text.Contains('+', StringComparison.Ordinal) || text.EndsWith('Z') || text.LastIndexOf('-') > 9)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>Escapes a value for a query string.</summary>
    public static string Query(string value) => Uri.EscapeDataString(value);

    /// <summary>Formats an instant for a query string.</summary>
    public static string Query(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>Builds a JSON schema object for a tool definition.</summary>
    public static JsonElement Schema(object properties, params string[] required) =>
        JsonSerializer.SerializeToElement(new { type = "object", properties, required, additionalProperties = false });
}
