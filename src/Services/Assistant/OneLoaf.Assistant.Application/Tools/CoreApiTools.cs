using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Core API routes used by the tools.</summary>
public static class CoreApiRoutes
{
    /// <summary>Base path.</summary>
    public const string Base = "/api/v1";

    /// <summary>Recipes.</summary>
    public const string Recipes = Base + "/recipes";

    /// <summary>Starters.</summary>
    public const string Starters = Base + "/starters";

    /// <summary>Batches.</summary>
    public const string Batches = Base + "/batches";

    /// <summary>Plan preview.</summary>
    public const string BatchPreview = Batches + "/preview";

    /// <summary>Baking history.</summary>
    public const string BakeRecords = Base + "/bake-records";

    /// <summary>Ingredients.</summary>
    public const string Ingredients = Base + "/ingredients";

    /// <summary>Data quality warnings.</summary>
    public const string Warnings = Base + "/warnings";

    /// <summary>Kitchen settings (time zone).</summary>
    public const string KitchenSettings = Base + "/kitchen-settings";
}

/// <summary>A tool that is one read call to the Core API. The Core API checks ownership and does every calculation.</summary>
public abstract class CoreApiTool(ICoreApiClient coreApi) : IAssistantTool
{
    /// <summary>Upper bound of loaves accepted from the model.</summary>
    protected const int MaxLoaves = 1000;

    /// <inheritdoc />
    public abstract ClaudeToolDefinition Definition { get; }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = BuildPath(input, out var problem);
        if (path is null)
        {
            return ToolResult.Error(problem ?? "Invalid input.");
        }

        return ToResult(await coreApi.SendAsync(HttpMethod.Get, path, null, cancellationToken));
    }

    /// <summary>The path and query for the input, or null with an problem for invalid input.</summary>
    protected abstract string? BuildPath(JsonElement input, out string? problem);

    /// <summary>Turns a Core API response into a tool result (problem details become an problem the model can explain).</summary>
    internal static ToolResult ToResult(CoreApiResponse response)
    {
        if (response.IsSuccess)
        {
            return new ToolResult(response.Body, false);
        }

        var title = "The request failed.";
        try
        {
            using var problem = JsonDocument.Parse(response.Body);
            if (problem.RootElement.TryGetProperty("title", out var t) && t.GetString() is { } text)
            {
                title = problem.RootElement.TryGetProperty("detail", out var d) && d.GetString() is { } detail ? $"{text} {detail}" : text;
            }
        }
        catch (JsonException)
        {
            // Not a problem document; keep the generic message.
        }

        return ToolResult.Error($"{(int)response.Status}: {title}");
    }
}

/// <summary><c>find_recipes</c>.</summary>
public sealed class FindRecipesTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    private const int MaxResults = 10;

    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.FindRecipes,
        "Finds the baker's recipes by words of the title and/or minimum flour protein percent. Use it to resolve a recipe name to its id.",
        Schema(new
        {
            query = new { type = "string", description = "Words of the recipe title." },
            minProteinPercent = new { type = "number", description = "Only recipes with a flour of at least this protein %." },
            limit = new { type = "integer", minimum = 1, maximum = MaxResults },
        }));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = null;
        var query = $"{CoreApiRoutes.Recipes}?pageSize={(TryGetInt(input, "limit", 1, MaxResults, out var limit) ? limit : MaxResults)}";
        if (GetString(input, "query") is { Length: > 0 } search)
        {
            query += $"&search={Query(search)}";
        }

        if (GetDecimal(input, "minProteinPercent") is { } protein)
        {
            query += $"&minProteinPercent={protein.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        return query;
    }
}

/// <summary><c>get_recipe</c>.</summary>
public sealed class GetRecipeTool(ICoreApiClient coreApi) : CoreApiTool(coreApi)
{
    /// <inheritdoc />
    public override ClaudeToolDefinition Definition { get; } = new(ToolNames.GetRecipe,
        "Returns a recipe card: yield, ingredients with quantities and units, stages with durations.",
        Schema(new { recipeId = new { type = "string", format = "uuid" } }, "recipeId"));

    /// <inheritdoc />
    protected override string? BuildPath(JsonElement input, out string? problem)
    {
        problem = "recipeId must be a UUID.";
        return TryGetGuid(input, "recipeId", out var id) ? $"{CoreApiRoutes.Recipes}/{id}" : null;
    }
}

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

/// <summary>Batch input shared by <c>preview_batch_plan</c> and <c>propose_batch</c>.</summary>
public static class BatchInput
{
    /// <summary>Upper bound of items.</summary>
    public const int MaxItems = 20;

    /// <summary>Schema of the batch input.</summary>
    public static readonly JsonElement Schema = ToolInput.Schema(new
    {
        name = new { type = "string" },
        plannedStartAt = new { type = "string", format = "date-time", description = "ISO 8601 with offset." },
        items = new
        {
            type = "array",
            minItems = 1,
            maxItems = MaxItems,
            items = new
            {
                type = "object",
                properties = new
                {
                    recipeId = new { type = "string", format = "uuid" },
                    loaves = new { type = "integer", minimum = 1, maximum = 1000 },
                    starterId = new { type = "string", format = "uuid" },
                },
                required = new[] { "recipeId", "loaves" },
            },
        },
    }, "plannedStartAt", "items");

    /// <summary>The Core API batch request for the input, or null with an problem.</summary>
    public static Dictionary<string, object?>? ToRequest(JsonElement input, string source, out string? problem)
    {
        problem = "plannedStartAt must be ISO 8601 with offset; items need recipeId (UUID) and loaves 1-1000.";
        if (!TryGetInstant(input, "plannedStartAt", out var start) || !input.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() is 0 or > MaxItems)
        {
            return null;
        }

        var list = new List<Dictionary<string, object?>>();
        foreach (var item in items.EnumerateArray())
        {
            if (!TryGetGuid(item, "recipeId", out var recipeId) || !TryGetInt(item, "loaves", 1, 1000, out var loaves))
            {
                return null;
            }

            var entry = new Dictionary<string, object?>(StringComparer.Ordinal) { ["recipeId"] = recipeId, ["loaves"] = loaves };
            if (TryGetGuid(item, "starterId", out var starterId))
            {
                entry["starterId"] = starterId;
                entry["starterReminderMinutes"] = DefaultStarterReminderMinutes;
            }

            list.Add(entry);
        }

        problem = null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = GetString(input, "name") is { Length: > 0 } name ? name : DefaultName,
            ["plannedStartAt"] = start,
            ["items"] = list,
            ["source"] = source,
        };
    }

    /// <summary>Name of a batch the model did not name.</summary>
    public const string DefaultName = "Batch planned with the assistant";

    /// <summary>Starter reminder before kneading for items with a starter.</summary>
    public const int DefaultStarterReminderMinutes = 60;

    /// <summary>Source value of a manual preview.</summary>
    public const string ManualSource = "manual";

    /// <summary>Source value of a batch saved from the assistant.</summary>
    public const string AssistantSource = "assistant";
}

/// <summary><c>preview_batch_plan</c>: the full plan from the database without saving.</summary>
public sealed class PreviewBatchPlanTool(ICoreApiClient coreApi) : IAssistantTool
{
    /// <inheritdoc />
    public ClaudeToolDefinition Definition { get; } = new(ToolNames.PreviewBatchPlan,
        "Builds the full plan of a batch (stages with times, ready window, kneading cycles, starter amounts, cost) without saving it.",
        BatchInput.Schema);

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        var request = BatchInput.ToRequest(input, BatchInput.ManualSource, out var problem);
        return request is null
            ? ToolResult.Error(problem!)
            : CoreApiTool.ToResult(await coreApi.SendAsync(HttpMethod.Post, CoreApiRoutes.BatchPreview, request, cancellationToken));
    }
}

/// <summary>
/// <c>propose_batch</c>: previews the plan and stores it as a pending action. Nothing is saved until the baker
/// confirms it in the UI.
/// </summary>
public sealed class ProposeBatchTool(ICoreApiClient coreApi, IPendingActionStore pendingActions, TimeProvider timeProvider)
    : IAssistantTool
{
    /// <summary>How long a proposal can be confirmed.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public ClaudeToolDefinition Definition { get; } = new(ToolNames.ProposeBatch,
        "Prepares a batch for the baker to confirm: returns actionId and the plan. Does NOT save the batch; the baker confirms it in the app.",
        BatchInput.Schema);

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var preview = BatchInput.ToRequest(input, BatchInput.ManualSource, out var problem);
        if (preview is null)
        {
            return ToolResult.Error(problem!);
        }

        var response = await coreApi.SendAsync(HttpMethod.Post, CoreApiRoutes.BatchPreview, preview, cancellationToken);
        if (!response.IsSuccess)
        {
            return CoreApiTool.ToResult(response);
        }

        var save = new Dictionary<string, object?>(preview, StringComparer.Ordinal) { ["source"] = BatchInput.AssistantSource };
        var now = timeProvider.GetUtcNow();
        var action = new PendingAction(Guid.NewGuid(), context.UserId, context.ConversationId, PendingActionKinds.CreateBatch,
            JsonSerializer.Serialize(save), now, now.Add(Lifetime));
        await pendingActions.SaveAsync(action, Lifetime, cancellationToken);

        using var plan = JsonDocument.Parse(response.Body);
        context.PendingAction = new PendingActionDto(action.Id, action.Kind, action.ExpiresAt, plan.RootElement.Clone());
        return new ToolResult(JsonSerializer.Serialize(new { actionId = action.Id, plan = plan.RootElement }), false);
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
