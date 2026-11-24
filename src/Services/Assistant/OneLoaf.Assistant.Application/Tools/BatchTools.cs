using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

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
                entry["remindToFeedStarter"] = true;
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
