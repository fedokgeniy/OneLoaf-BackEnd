using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Input of <c>propose_recipe</c>: a complete recipe with ingredients given by name.</summary>
public static class RecipeDraftInput
{
    /// <summary>Upper bound of ingredient lines and of stages.</summary>
    public const int MaxLines = 30;

    /// <summary>Ingredient categories of the Core API.</summary>
    public static readonly string[] Categories =
        ["flour", "water", "salt", "starter", "yeast", "sweetener", "fat", "dairy", "add_in", "other"];

    /// <summary>Units of the Core API.</summary>
    public static readonly string[] Units = ["g", "kg", "ml", "l", "pcs"];

    /// <summary>Stage types of the Core API.</summary>
    public static readonly string[] StageTypes =
        ["autolyse", "mix", "bulk_fermentation", "fold", "preshape", "shape", "proof", "bake", "cool", "other"];

    private const string Problem =
        "A recipe needs a title, yieldLoaves 1-1000, 1-30 ingredients (name, category, unit, quantity above 0) " +
        "and 1-30 stages (name, stageType, minMinutes <= maxMinutes).";

    /// <summary>Schema of the recipe input.</summary>
    public static readonly JsonElement Schema = ToolInput.Schema(new
    {
        title = new { type = "string", maxLength = 200 },
        description = new { type = "string", maxLength = 1000 },
        yieldLoaves = new { type = "integer", minimum = 1, maximum = 1000 },
        loafWeightG = new { type = "number", exclusiveMinimum = 0 },
        ingredients = new
        {
            type = "array",
            minItems = 1,
            maxItems = MaxLines,
            items = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", maxLength = 200, description = "Plain ingredient name, e.g. \"Bread flour\"." },
                    category = new { type = "string", @enum = Categories },
                    unit = new { type = "string", @enum = Units, description = "Use g for flour, water, salt and starter." },
                    quantity = new { type = "number", exclusiveMinimum = 0, description = "Quantity for yieldLoaves loaves, in the unit." },
                    isOptional = new { type = "boolean" },
                },
                required = new[] { "name", "category", "unit", "quantity" },
            },
        },
        stages = new
        {
            type = "array",
            minItems = 1,
            maxItems = MaxLines,
            items = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", maxLength = 100 },
                    stageType = new { type = "string", @enum = StageTypes },
                    minMinutes = new { type = "integer", minimum = 0, maximum = 10080 },
                    maxMinutes = new { type = "integer", minimum = 0, maximum = 10080 },
                    activeMinutes = new { type = "integer", minimum = 0, description = "Hands-on time, not more than maxMinutes." },
                    temperatureC = new { type = "number", minimum = -30, maximum = 350 },
                    instructions = new { type = "string", maxLength = 1000 },
                },
                required = new[] { "name", "stageType", "minMinutes", "maxMinutes" },
            },
        },
    }, "title", "yieldLoaves", "ingredients", "stages");

    /// <summary>The Core API recipe draft for the input, or null with a problem.</summary>
    public static Dictionary<string, object?>? ToRequest(JsonElement input, out string? problem)
    {
        problem = Problem;
        if (GetString(input, "title") is not { Length: > 0 } title || string.IsNullOrWhiteSpace(title)
            || !TryGetInt(input, "yieldLoaves", 1, 1000, out var yield)
            || !TryGetList(input, "ingredients", out var ingredientItems) || !TryGetList(input, "stages", out var stageItems))
        {
            return null;
        }

        var ingredients = new List<Dictionary<string, object?>>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ingredientItems)
        {
            var name = GetString(item, "name")?.Trim();
            var quantity = GetDecimal(item, "quantity") ?? 0m;
            if (string.IsNullOrEmpty(name) || !names.Add(name) || !IsOneOf(item, "category", Categories, out var category)
                || !IsOneOf(item, "unit", Units, out var unit) || quantity <= 0)
            {
                return null;
            }

            ingredients.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["categoryCode"] = category,
                ["unitCode"] = unit,
                ["quantity"] = quantity,
                ["isOptional"] = item.TryGetProperty("isOptional", out var optional) && optional.ValueKind == JsonValueKind.True,
            });
        }

        var stages = new List<Dictionary<string, object?>>();
        foreach (var item in stageItems)
        {
            var name = GetString(item, "name")?.Trim();
            if (string.IsNullOrEmpty(name) || !IsOneOf(item, "stageType", StageTypes, out var type)
                || !TryGetInt(item, "minMinutes", 0, 10080, out var min) || !TryGetInt(item, "maxMinutes", min, 10080, out var max))
            {
                return null;
            }

            var stage = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["stageTypeCode"] = type,
                ["minDurationMinutes"] = min,
                ["maxDurationMinutes"] = max,
                ["activeMinutes"] = TryGetInt(item, "activeMinutes", 0, max, out var active) ? active : 0,
            };
            if (GetDecimal(item, "temperatureC") is { } temperature and >= -30 and <= 350)
            {
                stage["temperatureC"] = temperature;
            }

            if (GetString(item, "instructions") is { Length: > 0 } instructions)
            {
                stage["instructions"] = instructions;
            }

            stages.Add(stage);
        }

        problem = null;
        var request = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = title.Trim(),
            ["description"] = GetString(input, "description"),
            ["yieldLoaves"] = yield,
            ["loafWeightG"] = GetDecimal(input, "loafWeightG") is > 0 and var weight ? weight : null,
            ["notes"] = RecipeNote,
            ["ingredients"] = ingredients,
            ["stages"] = stages,
        };
        return request;
    }

    /// <summary>Note stored with a recipe the assistant proposed.</summary>
    public const string RecipeNote = "Proposed by the OneLoaf assistant. Check the quantities and adjust them to your flour.";

    private static bool TryGetList(JsonElement input, string name, out List<JsonElement> items)
    {
        items = [];
        if (!input.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array
            || array.GetArrayLength() is 0 or > MaxLines)
        {
            return false;
        }

        items.AddRange(array.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object));
        return items.Count == array.GetArrayLength();
    }

    private static bool IsOneOf(JsonElement item, string name, string[] allowed, out string value)
    {
        value = GetString(item, name) ?? string.Empty;
        return Array.IndexOf(allowed, value) >= 0;
    }
}

/// <summary>
/// <c>propose_recipe</c>: checks a recipe written by the model, marks the ingredients the baker does not have yet and
/// stores it as a pending action. Nothing is saved until the baker confirms it in the UI.
/// </summary>
public sealed class ProposeRecipeTool(ICoreApiClient coreApi, IPendingActionStore pendingActions, TimeProvider timeProvider)
    : IAssistantTool
{
    /// <inheritdoc />
    public ClaudeToolDefinition Definition { get; } = new(ToolNames.ProposeRecipe,
        "Prepares a new recipe for the baker to confirm: returns actionId and the recipe with isNew for ingredients that " +
        "will be added to the catalog without a price. Does NOT save the recipe; the baker confirms it in the app.",
        RecipeDraftInput.Schema);

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = RecipeDraftInput.ToRequest(input, out var problem);
        if (request is null)
        {
            return ToolResult.Error(problem!);
        }

        var catalog = await coreApi.SendAsync(HttpMethod.Get, $"{CoreApiRoutes.Ingredients}?pageSize=100", null, cancellationToken);
        if (!catalog.IsSuccess)
        {
            return CoreApiTool.ToResult(catalog);
        }

        var existing = ExistingNames(catalog.Body);
        var ingredients = (List<Dictionary<string, object?>>)request["ingredients"]!;
        var shown = ingredients.Select(i => new Dictionary<string, object?>(i, StringComparer.Ordinal)
        {
            ["isNew"] = !existing.Contains((string)i["name"]!),
        }).ToList();
        var recipe = new Dictionary<string, object?>(request, StringComparer.Ordinal) { ["ingredients"] = shown };

        var now = timeProvider.GetUtcNow();
        var action = new PendingAction(Guid.NewGuid(), context.UserId, context.ConversationId, PendingActionKinds.CreateRecipe,
            JsonSerializer.Serialize(request), now, now.Add(ProposeBatchTool.Lifetime));
        await pendingActions.SaveAsync(action, ProposeBatchTool.Lifetime, cancellationToken);

        var plan = JsonSerializer.SerializeToElement(recipe);
        context.PendingAction = new PendingActionDto(action.Id, action.Kind, action.ExpiresAt, plan);
        return new ToolResult(JsonSerializer.Serialize(new { actionId = action.Id, recipe = plan }), false);
    }

    private static HashSet<string> ExistingNames(string body)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var page = JsonDocument.Parse(body);
        if (page.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (GetString(item, "name") is { } name)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }
}
