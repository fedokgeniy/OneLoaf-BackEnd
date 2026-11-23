using FluentValidation;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Batches;

/// <summary>Body of <c>POST /batches/{id}/save-as-variant</c>.</summary>
/// <param name="ItemId">Batch item whose recipe gets the variant; the first item when omitted.</param>
/// <param name="Title">Title of the new variant; "recipe title (batch name)" when omitted.</param>
public sealed record SaveAsVariantRequest(Guid? ItemId = null, string? Title = null);

/// <summary>A stage whose duration changed in the variant.</summary>
/// <param name="Name">Stage name.</param>
/// <param name="RecipeMinMinutes">Minimum in the recipe.</param>
/// <param name="RecipeMaxMinutes">Maximum in the recipe.</param>
/// <param name="VariantMinMinutes">Minimum in the new variant.</param>
/// <param name="VariantMaxMinutes">Maximum in the new variant.</param>
/// <param name="Source">actual (measured start to end) or planned (durations changed or extended in the batch).</param>
public sealed record StageChangeDto(
    string Name, int RecipeMinMinutes, int RecipeMaxMinutes, int VariantMinMinutes, int VariantMaxMinutes, string Source);

/// <summary>Result of saving a batch as a variant (US-25).</summary>
/// <param name="Recipe">The new variant.</param>
/// <param name="Changes">Stages that differ from the parent recipe.</param>
public sealed record SaveAsVariantResponse(RecipeDto Recipe, IReadOnlyList<StageChangeDto> Changes);

/// <summary>Where a stage duration of the variant comes from.</summary>
public static class StageChangeSources
{
    /// <summary>Measured start to end of a finished stage.</summary>
    public const string Actual = "actual";

    /// <summary>Durations changed or extended in the batch.</summary>
    public const string Planned = "planned";
}

/// <summary>Validates <see cref="SaveAsVariantRequest"/>.</summary>
public sealed class SaveAsVariantRequestValidator : AbstractValidator<SaveAsVariantRequest>
{
    /// <summary>Creates the rules.</summary>
    public SaveAsVariantRequestValidator() =>
        RuleFor(x => x.Title).MaximumLength(RecipeVariants.MaxTitleLength).Must(t => t is null || !string.IsNullOrWhiteSpace(t))
            .WithMessage("title must not be blank.");
}

/// <summary>Recipe limits used by the variant creation.</summary>
public static class RecipeVariants
{
    /// <summary>Longest recipe title.</summary>
    public const int MaxTitleLength = 200;
}

/// <summary>
/// Saves how a batch was really baked as a new variant of its recipe (US-25): finished stages take their measured
/// duration, pending or running stages whose durations were changed or extended take the batch's planned durations.
/// Ingredients and the other stages are inherited unchanged.
/// </summary>
public sealed class BatchVariantService(
    IBatchRepository batches, RecipeService recipes, ICurrentUser currentUser)
{
    /// <summary>Message when nothing differs from the recipe.</summary>
    public const string NoDivergenceMessage = "The batch followed the recipe; there is nothing to save as a variant.";

    /// <summary>Creates the variant; 422 when the batch did not diverge from the recipe.</summary>
    public async Task<SaveAsVariantResponse> SaveAsync(Guid batchId, SaveAsVariantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await batches.GetAsync(currentUser.Id, batchId, cancellationToken)
                    ?? throw new NotFoundException(BatchService.NotFoundMessage);
        var item = request.ItemId is { } itemId
            ? batch.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new NotFoundException("The batch item was not found.")
            : batch.Items.OrderBy(i => i.SortOrder).First();

        var recipe = await recipes.GetAsync(item.RecipeId, cancellationToken);
        var changes = new Dictionary<Guid, (int Min, int Max, string Source)>();
        foreach (var stage in item.Stages)
        {
            var state = await batches.GetStageAsync(currentUser.Id, batchId, stage.Id, cancellationToken);
            if (state?.RecipeStageId is not { } recipeStageId || recipe.Stages.FirstOrDefault(s => s.LineId == recipeStageId) is not { } line)
            {
                continue;
            }

            var proposed = Proposed(stage);
            if (proposed is { } p && (p.Min != line.MinDurationMinutes || p.Max != line.MaxDurationMinutes))
            {
                changes[recipeStageId] = p;
            }
        }

        if (changes.Count == 0)
        {
            throw new BusinessRuleException(NoDivergenceMessage);
        }

        var title = request.Title?.Trim() ?? Truncate($"{recipe.Title} ({batch.Name})");
        var variant = await recipes.CreateVariantAsync(recipe.Id, new CreateVariantRequest(title), cancellationToken);
        var content = new RecipeRequest(
            variant.Title, variant.Description, variant.YieldLoaves, variant.LoafWeightG, variant.Notes,
            variant.Ingredients.Select(i => new RecipeIngredientInput(i.LineId, i.IngredientId, i.Quantity, i.IsOptional, i.Note, i.SortOrder)).ToList(),
            variant.Stages.Select(s =>
            {
                var (min, max) = changes.TryGetValue(s.LineId, out var c) ? (c.Min, c.Max) : (s.MinDurationMinutes, s.MaxDurationMinutes);
                return new RecipeStageInput(s.LineId, s.Name, s.StageTypeCode, min, max, Math.Min(s.ActiveMinutes, max), s.ProofMode,
                    s.TemperatureC, s.UsesOven, s.UsesMixer, s.IsOptional, s.Instructions, s.SortOrder);
            }).ToList());
        var saved = await recipes.UpdateAsync(variant.Id, content, cancellationToken);

        var report = recipe.Stages.Where(s => changes.ContainsKey(s.LineId))
            .Select(s => new StageChangeDto(s.Name, s.MinDurationMinutes, s.MaxDurationMinutes, changes[s.LineId].Min,
                changes[s.LineId].Max, changes[s.LineId].Source))
            .ToList();
        return new SaveAsVariantResponse(saved, report);
    }

    /// <summary>Durations the variant takes from a batch stage, or null when the stage tells nothing new.</summary>
    public static (int Min, int Max, string Source)? Proposed(BatchStageDto stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        if (stage.Status == StageStatus.Done && stage.ActualStartAt is { } start && stage.ActualEndAt is { } end && end > start)
        {
            var minutes = Math.Max(1, (int)Math.Round((end - start).TotalMinutes, MidpointRounding.AwayFromZero));
            return (minutes, minutes, StageChangeSources.Actual);
        }

        if (stage.Status is StageStatus.Pending or StageStatus.Running && (stage.IsOverridden || stage.ExtendedMinutes > 0))
        {
            return (stage.PlannedMinMinutes + stage.ExtendedMinutes, stage.PlannedMaxMinutes + stage.ExtendedMinutes, StageChangeSources.Planned);
        }

        return null;
    }

    private static string Truncate(string title) => title.Length <= RecipeVariants.MaxTitleLength ? title : title[..RecipeVariants.MaxTitleLength];
}
