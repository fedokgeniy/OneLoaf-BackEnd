using System.Text.Json;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary><c>calculate_cost</c>.</summary>
public sealed class CalculateCostTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.CalculateCost,
        "Cost breakdown of N loaves of a recipe with the prices valid on a date: ingredient lines, electricity, packaging, labor, loss and total. isComplete=false lists missing prices.",
        Schema(new
        {
            recipeId = new { type = "string", format = "uuid" },
            loaves = new { type = "integer", minimum = 1, maximum = MaxLoaves },
            asOf = new { type = "string", format = "date", description = "Price date yyyy-MM-dd; default today." },
        }, "recipeId", "loaves"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "recipeId must be a UUID and loaves 1-1000.";
        if (!TryGetGuid(input, "recipeId", out var id) || !TryGetInt(input, "loaves", 1, MaxLoaves, out var loaves))
        {
            return null;
        }

        var path = $"{CoreApiRoutes.Recipes}/{id}/cost?loaves={loaves}";
        return GetString(input, "asOf") is { } asOf && DateOnly.TryParse(asOf, System.Globalization.CultureInfo.InvariantCulture, out var date)
            ? $"{path}&asOf={date:yyyy-MM-dd}"
            : path;
    }
}

/// <summary><c>calculate_timeline</c>.</summary>
public sealed class CalculateTimelineTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.CalculateTimeline,
        "Stage timeline and ready window of a recipe for a start time.",
        Schema(new
        {
            recipeId = new { type = "string", format = "uuid" },
            start = new { type = "string", format = "date-time", description = "ISO 8601 with offset." },
        }, "recipeId", "start"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "recipeId must be a UUID and start an ISO 8601 time with offset.";
        return TryGetGuid(input, "recipeId", out var id) && TryGetInstant(input, "start", out var start)
            ? $"{CoreApiRoutes.Recipes}/{id}/timeline?start={Query(start)}"
            : null;
    }
}

/// <summary><c>calculate_start_window</c>.</summary>
public sealed class CalculateStartWindowTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.CalculateStartWindow,
        "When to start a recipe so that it is ready at a desired time.",
        Schema(new
        {
            recipeId = new { type = "string", format = "uuid" },
            readyAt = new { type = "string", format = "date-time", description = "ISO 8601 with offset." },
        }, "recipeId", "readyAt"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "recipeId must be a UUID and readyAt an ISO 8601 time with offset.";
        return TryGetGuid(input, "recipeId", out var id) && TryGetInstant(input, "readyAt", out var readyAt)
            ? $"{CoreApiRoutes.Recipes}/{id}/start-window?readyAt={Query(readyAt)}"
            : null;
    }
}

/// <summary><c>calculate_production_figures</c>.</summary>
public sealed class CalculateProductionFiguresTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.CalculateProductionFigures,
        "Dough weight (g), kneading cycles for the mixer bowl and starter amount (g) for N loaves.",
        Schema(new
        {
            recipeId = new { type = "string", format = "uuid" },
            loaves = new { type = "integer", minimum = 1, maximum = MaxLoaves },
        }, "recipeId", "loaves"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "recipeId must be a UUID and loaves 1-1000.";
        return TryGetGuid(input, "recipeId", out var id) && TryGetInt(input, "loaves", 1, MaxLoaves, out var loaves)
            ? $"{CoreApiRoutes.Recipes}/{id}/production-figures?loaves={loaves}"
            : null;
    }
}
