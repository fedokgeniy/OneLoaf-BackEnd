using System.Text.Json;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary><c>list_starters</c>.</summary>
public sealed class ListStartersTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.ListStarters,
        "The baker's starters with last feeding, next feeding and expected peak.", Schema(new { }));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = null;
        return $"{CoreApiRoutes.Starters}?pageSize=100";
    }
}

/// <summary><c>get_ingredient_prices</c>.</summary>
public sealed class GetIngredientPricesTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.GetIngredientPrices,
        "Price history of an ingredient, newest first, with the change in percent.",
        Schema(new { ingredientId = new { type = "string", format = "uuid" } }, "ingredientId"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "ingredientId must be a UUID.";
        return TryGetGuid(input, "ingredientId", out var id) ? $"{CoreApiRoutes.Ingredients}/{id}/prices" : null;
    }
}

/// <summary><c>get_missing_data</c>.</summary>
public sealed class GetMissingDataTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.GetMissingData,
        "Data quality warnings: missing prices, recipes without ingredients or stages, overdue batches and starters.", Schema(new { }));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = null;
        return CoreApiRoutes.Warnings;
    }
}

/// <summary><c>get_bake_analysis_data</c>: record with feedback and the planned vs actual stage log.</summary>
public sealed class GetBakeAnalysisDataTool(ICoreApiClient coreApi) : IAssistantTool
{
    /// <inheritdoc />
    public ClaudeToolDefinition Definition { get; } = new(ToolNames.GetBakeAnalysisData,
        "A bake record (items, rating, comment) and its stage log: planned vs actual times, deviations in minutes, temperatures.",
        Schema(new { bakeRecordId = new { type = "string", format = "uuid" } }, "bakeRecordId"));

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        if (!TryGetGuid(input, "bakeRecordId", out var id))
        {
            return ToolResult.Error("bakeRecordId must be a UUID.");
        }

        var record = await coreApi.SendAsync(HttpMethod.Get, $"{CoreApiRoutes.BakeRecords}/{id}", null, cancellationToken);
        if (!record.IsSuccess)
        {
            return CoreApiTool.ToResult(record);
        }

        var log = await coreApi.SendAsync(HttpMethod.Get, $"{CoreApiRoutes.BakeRecords}/{id}/stage-log", null, cancellationToken);
        if (!log.IsSuccess)
        {
            return CoreApiTool.ToResult(log);
        }

        using var recordJson = JsonDocument.Parse(record.Body);
        using var logJson = JsonDocument.Parse(log.Body);
        return new ToolResult(JsonSerializer.Serialize(new { record = recordJson.RootElement, stageLog = logJson.RootElement }), false);
    }
}
