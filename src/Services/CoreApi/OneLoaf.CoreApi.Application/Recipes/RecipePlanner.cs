using System.Text.Json;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.Application.Recipes;

/// <summary>
/// Turns the full content of a recipe from a request into the rows the recipe stores itself (US-05, US-07).
/// A base recipe stores every line. A variant stores only differences from the effective content of its parent:
/// a changed line becomes a replacement row, a missing parent line a removal row, a new line an added row, and a
/// line equal to the parent's is inherited (no row). Existing row ids are kept so that variants of this recipe keep
/// pointing at the same lines.
/// </summary>
public static class RecipePlanner
{
    /// <summary>Key of the description in the variant overrides JSON.</summary>
    public const string DescriptionKey = "description";

    /// <summary>Key of the loaf weight in the variant overrides JSON.</summary>
    public const string LoafWeightKey = "loafWeightG";

    /// <summary>Key of the notes in the variant overrides JSON.</summary>
    public const string NotesKey = "notes";

    /// <summary>Message when a variant changes the yield.</summary>
    public const string VariantYieldMessage = "A variant keeps the yield of its parent recipe.";

    /// <summary>Rows of a base recipe.</summary>
    public static RecipeChangeSet PlanBase(
        RecipeRequest request, RecipeState? existing, IReadOnlyDictionary<string, StageTypeDto> stageTypes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stageTypes);

        var ownIngredientIds = existing?.Ingredients.Select(r => r.Id).OfType<Guid>().ToHashSet() ?? [];
        var ownStageIds = existing?.Stages.Select(r => r.Id).OfType<Guid>().ToHashSet() ?? [];

        var ingredients = request.Ingredients.Select((line, index) => new OwnIngredientRow(
            KeepId(line.LineId, ownIngredientIds), line.IngredientId, line.Quantity, line.IsOptional,
            line.SortOrder ?? index + 1, Clean(line.Note), null, false)).ToList();

        var stages = request.Stages.Select((stage, index) => ToRow(stage, index,
            KeepId(stage.LineId, ownStageIds), stageTypes[stage.StageTypeCode], inherited: null, replaces: null)).ToList();

        var header = new RecipeHeaderChange(request.Title.Trim(), request.YieldLoaves, Clean(request.Description),
            request.LoafWeightG, Clean(request.Notes), null);
        return new RecipeChangeSet(header, ingredients, stages);
    }

    /// <summary>Rows of a variant: only the differences from the effective <paramref name="parent"/>.</summary>
    /// <exception cref="BusinessRuleException">The yield differs from the parent's.</exception>
    public static RecipeChangeSet PlanVariant(
        RecipeRequest request, RecipeState existing, RecipeDto parent, IReadOnlyDictionary<string, StageTypeDto> stageTypes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(stageTypes);

        if (request.YieldLoaves != parent.YieldLoaves)
        {
            throw new BusinessRuleException(VariantYieldMessage);
        }

        var header = new RecipeHeaderChange(request.Title.Trim(), parent.YieldLoaves, null, null, null,
            OverridesJson(request, parent));
        return new RecipeChangeSet(header, PlanIngredients(request, existing, parent), PlanStages(request, existing, parent, stageTypes));
    }

    /// <summary>Header and rows of a new variant that inherits everything.</summary>
    public static RecipeChangeSet PlanNewVariant(string title, RecipeDto parent)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(parent);
        return new RecipeChangeSet(new RecipeHeaderChange(title.Trim(), parent.YieldLoaves, null, null, null, null), [], []);
    }

    private static List<OwnIngredientRow> PlanIngredients(RecipeRequest request, RecipeState existing, RecipeDto parent)
    {
        var parentLines = parent.Ingredients.ToDictionary(l => l.LineId);
        var ownReplacements = existing.Ingredients.Where(r => r.ReplacesItemId.HasValue).ToList();
        var replacementByParent = ownReplacements.ToDictionary(r => r.ReplacesItemId!.Value, r => r.Id);
        var parentByReplacement = ownReplacements.Where(r => r.Id.HasValue).ToDictionary(r => r.Id!.Value, r => r.ReplacesItemId!.Value);
        var ownAdded = existing.Ingredients.Where(r => r.ReplacesItemId is null).Select(r => r.Id).OfType<Guid>().ToHashSet();

        var rows = new List<OwnIngredientRow>();
        var keptParentLines = new HashSet<Guid>();
        for (var index = 0; index < request.Ingredients.Count; index++)
        {
            var line = request.Ingredients[index];
            var sortOrder = line.SortOrder ?? index + 1;
            var note = Clean(line.Note);
            var parentLineId = ResolveParentLine(line.LineId, parentLines.ContainsKey, parentByReplacement);
            if (parentLineId is { } pid)
            {
                if (!keptParentLines.Add(pid))
                {
                    throw DuplicateLine(pid);
                }

                var p = parentLines[pid];
                var unchanged = p.IngredientId == line.IngredientId && p.Quantity == line.Quantity
                    && p.IsOptional == line.IsOptional && p.SortOrder == sortOrder && Clean(p.Note) == note;
                if (!unchanged)
                {
                    rows.Add(new OwnIngredientRow(replacementByParent.GetValueOrDefault(pid), line.IngredientId,
                        line.Quantity, line.IsOptional, sortOrder, note, pid, false));
                }
            }
            else
            {
                rows.Add(new OwnIngredientRow(KeepId(line.LineId, ownAdded), line.IngredientId, line.Quantity,
                    line.IsOptional, sortOrder, note, null, false));
            }
        }

        foreach (var removed in parent.Ingredients.Where(p => !keptParentLines.Contains(p.LineId)))
        {
            rows.Add(new OwnIngredientRow(replacementByParent.GetValueOrDefault(removed.LineId), removed.IngredientId,
                removed.Quantity, removed.IsOptional, removed.SortOrder, removed.Note, removed.LineId, true));
        }

        return rows;
    }

    private static List<OwnStageRow> PlanStages(
        RecipeRequest request, RecipeState existing, RecipeDto parent, IReadOnlyDictionary<string, StageTypeDto> stageTypes)
    {
        var parentStages = parent.Stages.ToDictionary(s => s.LineId);
        var ownReplacements = existing.Stages.Where(r => r.ReplacesStageId.HasValue).ToList();
        var replacementByParent = ownReplacements.ToDictionary(r => r.ReplacesStageId!.Value, r => r.Id);
        var parentByReplacement = ownReplacements.Where(r => r.Id.HasValue).ToDictionary(r => r.Id!.Value, r => r.ReplacesStageId!.Value);
        var ownAdded = existing.Stages.Where(r => r.ReplacesStageId is null).Select(r => r.Id).OfType<Guid>().ToHashSet();

        var rows = new List<OwnStageRow>();
        var keptParentStages = new HashSet<Guid>();
        for (var index = 0; index < request.Stages.Count; index++)
        {
            var stage = request.Stages[index];
            var parentStageId = ResolveParentLine(stage.LineId, parentStages.ContainsKey, parentByReplacement);
            if (parentStageId is { } pid)
            {
                if (!keptParentStages.Add(pid))
                {
                    throw DuplicateLine(pid);
                }

                var p = parentStages[pid];
                var row = ToRow(stage, index, replacementByParent.GetValueOrDefault(pid), stageTypes[stage.StageTypeCode], p, pid);
                if (!SameAs(row, p))
                {
                    rows.Add(row);
                }
            }
            else
            {
                rows.Add(ToRow(stage, index, KeepId(stage.LineId, ownAdded), stageTypes[stage.StageTypeCode], null, null));
            }
        }

        foreach (var removed in parent.Stages.Where(p => !keptParentStages.Contains(p.LineId)))
        {
            rows.Add(new OwnStageRow(replacementByParent.GetValueOrDefault(removed.LineId), removed.SortOrder, removed.Name,
                removed.StageTypeCode, removed.MinDurationMinutes, removed.MaxDurationMinutes, removed.ActiveMinutes,
                removed.ProofMode, removed.TemperatureC, removed.UsesOven, removed.UsesMixer, removed.IsOptional,
                removed.Instructions, removed.LineId, true));
        }

        return rows;
    }

    private static OwnStageRow ToRow(
        RecipeStageInput stage, int index, Guid? id, StageTypeDto type, RecipeStageDto? inherited, Guid? replaces) =>
        new(id, stage.SortOrder ?? index + 1, stage.Name.Trim(), stage.StageTypeCode, stage.MinDurationMinutes,
            stage.MaxDurationMinutes, stage.ActiveMinutes, type.IsFermentation ? stage.ProofMode : null, stage.TemperatureC,
            stage.UsesOven ?? inherited?.UsesOven ?? type.DefaultUsesOven,
            stage.UsesMixer ?? inherited?.UsesMixer ?? type.DefaultUsesMixer,
            stage.IsOptional, Clean(stage.Instructions), replaces, false);

    private static bool SameAs(OwnStageRow row, RecipeStageDto p) =>
        row.SortOrder == p.SortOrder && row.Name == p.Name && row.StageTypeCode == p.StageTypeCode
        && row.MinDurationMinutes == p.MinDurationMinutes && row.MaxDurationMinutes == p.MaxDurationMinutes
        && row.ActiveMinutes == p.ActiveMinutes && row.ProofMode == p.ProofMode && row.TemperatureC == p.TemperatureC
        && row.UsesOven == p.UsesOven && row.UsesMixer == p.UsesMixer && row.IsOptional == p.IsOptional
        && row.Instructions == Clean(p.Instructions);

    /// <summary>
    /// The parent line a requested line refers to: either a parent line id directly, or the id of this variant's own
    /// replacement row (what GET returns for a changed line).
    /// </summary>
    private static Guid? ResolveParentLine(Guid? lineId, Func<Guid, bool> isParentLine, Dictionary<Guid, Guid> parentByReplacement)
    {
        if (lineId is not { } id)
        {
            return null;
        }

        if (parentByReplacement.TryGetValue(id, out var parentLine))
        {
            return parentLine;
        }

        return isParentLine(id) ? id : null;
    }

    private static string? OverridesJson(RecipeRequest request, RecipeDto parent)
    {
        var overrides = new Dictionary<string, object?>(StringComparer.Ordinal);
        var description = Clean(request.Description);
        var notes = Clean(request.Notes);
        if (description != Clean(parent.Description))
        {
            overrides[DescriptionKey] = description;
        }

        if (request.LoafWeightG != parent.LoafWeightG)
        {
            overrides[LoafWeightKey] = request.LoafWeightG;
        }

        if (notes != Clean(parent.Notes))
        {
            overrides[NotesKey] = notes;
        }

        return overrides.Count == 0 ? null : JsonSerializer.Serialize(overrides);
    }

    private static Guid? KeepId(Guid? requested, HashSet<Guid> own) =>
        requested is { } id && own.Contains(id) ? id : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static FluentValidation.ValidationException DuplicateLine(Guid lineId) =>
        Common.ValidationFailures.For("lineId", $"Line {lineId} is listed more than once.");
}
