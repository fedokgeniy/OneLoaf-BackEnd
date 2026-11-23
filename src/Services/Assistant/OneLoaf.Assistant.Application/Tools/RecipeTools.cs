using System.Text.Json;
using OneLoaf.Assistant.Application.Claude;
using static OneLoaf.Assistant.Application.Tools.ToolInput;

namespace OneLoaf.Assistant.Application.Tools;

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
