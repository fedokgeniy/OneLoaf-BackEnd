namespace OneLoaf.CoreApi.Application.Recipes;

/// <summary>Views of a recipe computed in C#: scaling (US-06) and comparison of a variant with its parent (US-08).</summary>
public static class RecipeViews
{
    private const int QuantityDigits = 3;

    /// <summary>Ingredient quantities for <paramref name="loaves"/> loaves: quantity × loaves / yield.</summary>
    public static ScaledRecipeDto Scale(RecipeDto recipe, int loaves)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(loaves);

        var lines = recipe.Ingredients
            .OrderBy(l => l.SortOrder)
            .Select(l => new ScaledIngredientDto(l.LineId, l.IngredientId, l.IngredientName, l.UnitCode,
                Math.Round(l.Quantity * loaves / recipe.YieldLoaves, QuantityDigits, MidpointRounding.AwayFromZero),
                l.IsOptional))
            .ToList();
        return new ScaledRecipeDto(recipe.Id, recipe.YieldLoaves, loaves, lines);
    }

    /// <summary>
    /// Every parent line with its fate in the variant (unchanged, changed, removed) followed by the lines the
    /// variant adds; and the scalar fields that differ.
    /// </summary>
    public static RecipeComparisonDto Compare(RecipeDto variant, RecipeState variantState, RecipeDto parent)
    {
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(variantState);
        ArgumentNullException.ThrowIfNull(parent);

        var variantLines = variant.Ingredients.ToDictionary(l => l.LineId);
        var removedLines = variantState.Ingredients.Where(r => r.IsRemoved).Select(r => r.ReplacesItemId!.Value).ToHashSet();
        var replacedLines = variantState.Ingredients.Where(r => !r.IsRemoved && r.ReplacesItemId.HasValue && r.Id.HasValue)
            .ToDictionary(r => r.ReplacesItemId!.Value, r => r.Id!.Value);

        var ingredients = new List<IngredientComparisonDto>();
        foreach (var p in parent.Ingredients.OrderBy(l => l.SortOrder))
        {
            if (removedLines.Contains(p.LineId))
            {
                ingredients.Add(new IngredientComparisonDto(ChangeKind.Removed, p, null));
            }
            else if (replacedLines.TryGetValue(p.LineId, out var replacementId))
            {
                ingredients.Add(new IngredientComparisonDto(ChangeKind.Changed, p, variantLines.GetValueOrDefault(replacementId)));
            }
            else
            {
                ingredients.Add(new IngredientComparisonDto(ChangeKind.Unchanged, p, variantLines.GetValueOrDefault(p.LineId)));
            }
        }

        var replacementIds = replacedLines.Values.ToHashSet();
        ingredients.AddRange(variant.Ingredients
            .Where(l => !l.IsInherited && !replacementIds.Contains(l.LineId))
            .OrderBy(l => l.SortOrder)
            .Select(l => new IngredientComparisonDto(ChangeKind.Added, null, l)));

        var variantStages = variant.Stages.ToDictionary(s => s.LineId);
        var removedStages = variantState.Stages.Where(r => r.IsRemoved).Select(r => r.ReplacesStageId!.Value).ToHashSet();
        var replacedStages = variantState.Stages.Where(r => !r.IsRemoved && r.ReplacesStageId.HasValue && r.Id.HasValue)
            .ToDictionary(r => r.ReplacesStageId!.Value, r => r.Id!.Value);

        var stages = new List<StageComparisonDto>();
        foreach (var p in parent.Stages.OrderBy(s => s.SortOrder))
        {
            if (removedStages.Contains(p.LineId))
            {
                stages.Add(new StageComparisonDto(ChangeKind.Removed, p, null));
            }
            else if (replacedStages.TryGetValue(p.LineId, out var replacementId))
            {
                stages.Add(new StageComparisonDto(ChangeKind.Changed, p, variantStages.GetValueOrDefault(replacementId)));
            }
            else
            {
                stages.Add(new StageComparisonDto(ChangeKind.Unchanged, p, variantStages.GetValueOrDefault(p.LineId)));
            }
        }

        var stageReplacementIds = replacedStages.Values.ToHashSet();
        stages.AddRange(variant.Stages
            .Where(s => !s.IsInherited && !stageReplacementIds.Contains(s.LineId))
            .OrderBy(s => s.SortOrder)
            .Select(s => new StageComparisonDto(ChangeKind.Added, null, s)));

        return new RecipeComparisonDto(variant.Id, parent.Id, ChangedFields(variant, parent), ingredients, stages);
    }

    private static List<string> ChangedFields(RecipeDto variant, RecipeDto parent)
    {
        var fields = new List<string>();
        if (variant.Title != parent.Title)
        {
            fields.Add("title");
        }

        if (variant.Description != parent.Description)
        {
            fields.Add(RecipePlanner.DescriptionKey);
        }

        if (variant.LoafWeightG != parent.LoafWeightG)
        {
            fields.Add(RecipePlanner.LoafWeightKey);
        }

        if (variant.Notes != parent.Notes)
        {
            fields.Add(RecipePlanner.NotesKey);
        }

        return fields;
    }
}
