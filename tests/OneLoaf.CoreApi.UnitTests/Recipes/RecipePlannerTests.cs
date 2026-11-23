using System.Text.Json;
using FluentValidation;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.Recipes;

public sealed class RecipePlannerTests
{
    private static readonly Dictionary<string, StageTypeDto> StageTypes = new(StringComparer.Ordinal)
    {
        ["mix"] = new("mix", "Mixing", false, false, true),
        ["proof"] = new("proof", "Proofing", true, false, false),
        ["bake"] = new("bake", "Baking", false, true, false),
    };

    private static readonly Guid ParentId = Guid.NewGuid();
    private static readonly Guid Flour = Guid.NewGuid();
    private static readonly Guid Water = Guid.NewGuid();
    private static readonly Guid Salt = Guid.NewGuid();
    private static readonly Guid FlourLine = Guid.NewGuid();
    private static readonly Guid WaterLine = Guid.NewGuid();
    private static readonly Guid MixStage = Guid.NewGuid();
    private static readonly Guid BakeStage = Guid.NewGuid();

    private static readonly RecipeDto Parent = new(
        ParentId, null, "Loaf", "Basic", 1, 900m, null, false,
        [
            new(FlourLine, Flour, "Flour", "flour", "g", 500, false, 1, null, null, false),
            new(WaterLine, Water, "Water", "liquid", "g", 350, false, 2, "Warm", null, false),
        ],
        [
            new(MixStage, 1, "Mix", "mix", 10, 15, 10, null, null, false, true, false, null, false),
            new(BakeStage, 2, "Bake", "bake", 40, 45, 5, null, 230m, true, false, false, null, false),
        ],
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Fact]
    public void PlanBase_NewRecipe_InsertsEveryLineWithPositionAndStageDefaults()
    {
        var request = new RecipeRequest(" Loaf ", " ", 2, 900, null,
            [new(null, Flour, 500), new(null, Water, 350, Note: "  ")],
            [new(null, "Mix", "mix", 10, 15), new(null, "Bake", "bake", 40, 45, UsesMixer: true)]);

        var plan = RecipePlanner.PlanBase(request, null, StageTypes);

        Assert.Equal("Loaf", plan.Header.Title);
        Assert.Null(plan.Header.Description);
        Assert.Null(plan.Header.VariantOverridesJson);
        Assert.All(plan.Ingredients, r => Assert.Null(r.Id));
        Assert.Equal([1, 2], plan.Ingredients.Select(r => r.SortOrder));
        Assert.Null(plan.Ingredients[1].Note);
        Assert.True(plan.Stages[0].UsesMixer);
        Assert.False(plan.Stages[0].UsesOven);
        Assert.True(plan.Stages[1].UsesOven);
        Assert.True(plan.Stages[1].UsesMixer);
    }

    [Fact]
    public void PlanBase_ExistingRecipe_KeepsKnownLineIdsAndDropsForeignIds()
    {
        var existing = new RecipeState(ParentId, null, 1,
            [new(FlourLine, Flour, 500, false, 1, null, null, false)],
            [new(MixStage, 1, "Mix", "mix", 10, 15, 0, null, null, false, true, false, null, null, false)]);
        var foreign = Guid.NewGuid();
        var request = new RecipeRequest("Loaf", null, 1, null, null,
            [new(FlourLine, Flour, 550), new(foreign, Water, 300)],
            [new(MixStage, "Mix", "mix", 12, 15)]);

        var plan = RecipePlanner.PlanBase(request, existing, StageTypes);

        Assert.Equal(FlourLine, plan.Ingredients[0].Id);
        Assert.Null(plan.Ingredients[1].Id);
        Assert.Equal(MixStage, plan.Stages[0].Id);
    }

    [Fact]
    public void PlanBase_ProofModeOnNonFermentationStage_IsDropped()
    {
        var request = new RecipeRequest("Loaf", null, 1, null, null, [],
            [new(null, "Proof", "proof", 600, 720, ProofMode: ProofMode.Cold), new(null, "Mix", "mix", 1, 2, ProofMode: ProofMode.Warm)]);

        var plan = RecipePlanner.PlanBase(request, null, StageTypes);

        Assert.Equal(ProofMode.Cold, plan.Stages[0].ProofMode);
        Assert.Null(plan.Stages[1].ProofMode);
    }

    [Fact]
    public void PlanVariant_SameContentAsParent_StoresNothing()
    {
        var plan = RecipePlanner.PlanVariant(RequestFrom(Parent, "Variant"), EmptyState(), Parent, StageTypes);

        Assert.Empty(plan.Ingredients);
        Assert.Empty(plan.Stages);
        Assert.Null(plan.Header.VariantOverridesJson);
        Assert.Null(plan.Header.Description);
        Assert.Equal("Variant", plan.Header.Title);
    }

    [Fact]
    public void PlanVariant_ChangedLine_BecomesReplacementOfTheParentLine()
    {
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [new(FlourLine, Flour, 500, SortOrder: 1), new(WaterLine, Water, 380, Note: "Warm", SortOrder: 2)],
        };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        var row = Assert.Single(plan.Ingredients);
        Assert.Equal(WaterLine, row.ReplacesItemId);
        Assert.False(row.IsRemoved);
        Assert.Equal(380m, row.Quantity);
        Assert.Null(row.Id);
    }

    [Fact]
    public void PlanVariant_LineMissingFromRequest_BecomesRemovalRow()
    {
        var request = RequestFrom(Parent, "Variant") with { Ingredients = [new(FlourLine, Flour, 500, SortOrder: 1)] };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        var row = Assert.Single(plan.Ingredients);
        Assert.True(row.IsRemoved);
        Assert.Equal(WaterLine, row.ReplacesItemId);
        Assert.Equal(Water, row.IngredientId);
    }

    [Fact]
    public void PlanVariant_NewLine_BecomesAddedRow()
    {
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [.. RequestFrom(Parent, "Variant").Ingredients, new(null, Salt, 10, SortOrder: 3)],
        };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        var row = Assert.Single(plan.Ingredients);
        Assert.Null(row.ReplacesItemId);
        Assert.Equal(Salt, row.IngredientId);
    }

    [Fact]
    public void PlanVariant_ExistingReplacementSentBackById_IsUpdatedInPlace()
    {
        var replacementId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1,
            [new(replacementId, Water, 380, false, 2, "Warm", WaterLine, false)], []);
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [new(FlourLine, Flour, 500, SortOrder: 1), new(replacementId, Water, 400, Note: "Warm", SortOrder: 2)],
        };

        var plan = RecipePlanner.PlanVariant(request, state, Parent, StageTypes);

        var row = Assert.Single(plan.Ingredients);
        Assert.Equal(replacementId, row.Id);
        Assert.Equal(WaterLine, row.ReplacesItemId);
        Assert.Equal(400m, row.Quantity);
    }

    [Fact]
    public void PlanVariant_ReplacementRevertedToParentValues_IsDropped()
    {
        var replacementId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1,
            [new(replacementId, Water, 380, false, 2, "Warm", WaterLine, false)], []);
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [new(FlourLine, Flour, 500, SortOrder: 1), new(replacementId, Water, 350, Note: "Warm", SortOrder: 2)],
        };

        var plan = RecipePlanner.PlanVariant(request, state, Parent, StageTypes);

        Assert.Empty(plan.Ingredients);
    }

    [Fact]
    public void PlanVariant_ExistingAddedLine_KeepsItsId()
    {
        var addedId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1, [new(addedId, Salt, 10, false, 3, null, null, false)], []);
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [.. RequestFrom(Parent, "Variant").Ingredients, new(addedId, Salt, 12, SortOrder: 3)],
        };

        var plan = RecipePlanner.PlanVariant(request, state, Parent, StageTypes);

        Assert.Equal(addedId, Assert.Single(plan.Ingredients).Id);
    }

    [Fact]
    public void PlanVariant_RemovedParentLineWithExistingReplacement_ReusesTheRow()
    {
        var replacementId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1,
            [new(replacementId, Water, 380, false, 2, null, WaterLine, false)], []);
        var request = RequestFrom(Parent, "Variant") with { Ingredients = [new(FlourLine, Flour, 500, SortOrder: 1)] };

        var plan = RecipePlanner.PlanVariant(request, state, Parent, StageTypes);

        var row = Assert.Single(plan.Ingredients);
        Assert.Equal(replacementId, row.Id);
        Assert.True(row.IsRemoved);
    }

    [Fact]
    public void PlanVariant_StageChangesAndRemovals()
    {
        var request = RequestFrom(Parent, "Variant") with
        {
            Stages = [new(MixStage, "Mix", "mix", 10, 20, 10, UsesMixer: true, SortOrder: 1)],
        };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        Assert.Equal(2, plan.Stages.Count);
        var changed = plan.Stages.Single(s => !s.IsRemoved);
        Assert.Equal(MixStage, changed.ReplacesStageId);
        Assert.Equal(20, changed.MaxDurationMinutes);
        var removed = plan.Stages.Single(s => s.IsRemoved);
        Assert.Equal(BakeStage, removed.ReplacesStageId);
    }

    [Fact]
    public void PlanVariant_AddedStage_UsesTypeDefaults()
    {
        var request = RequestFrom(Parent, "Variant") with
        {
            Stages = [.. RequestFrom(Parent, "Variant").Stages, new(null, "Cold proof", "proof", 600, 720, ProofMode: ProofMode.Cold, SortOrder: 3)],
        };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        var added = Assert.Single(plan.Stages);
        Assert.Null(added.ReplacesStageId);
        Assert.Equal(ProofMode.Cold, added.ProofMode);
        Assert.False(added.UsesOven);
    }

    [Fact]
    public void PlanVariant_ChangedScalars_GoToOverridesJson()
    {
        var request = RequestFrom(Parent, "Variant") with { LoafWeightG = 920, Notes = "Denser", Description = "Basic" };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        using var json = JsonDocument.Parse(plan.Header.VariantOverridesJson!);
        Assert.Equal(920m, json.RootElement.GetProperty(RecipePlanner.LoafWeightKey).GetDecimal());
        Assert.Equal("Denser", json.RootElement.GetProperty(RecipePlanner.NotesKey).GetString());
        Assert.False(json.RootElement.TryGetProperty(RecipePlanner.DescriptionKey, out _));
        Assert.Null(plan.Header.LoafWeightG);
    }

    [Fact]
    public void PlanVariant_ClearedDescription_IsOverriddenWithNull()
    {
        var request = RequestFrom(Parent, "Variant") with { Description = null };

        var plan = RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes);

        using var json = JsonDocument.Parse(plan.Header.VariantOverridesJson!);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty(RecipePlanner.DescriptionKey).ValueKind);
    }

    [Fact]
    public void PlanVariant_DifferentYield_ThrowsBusinessRule()
    {
        var request = RequestFrom(Parent, "Variant") with { YieldLoaves = 2 };

        var exception = Assert.Throws<BusinessRuleException>(() => RecipePlanner.PlanVariant(request, EmptyState(), Parent, StageTypes));

        Assert.Equal(RecipePlanner.VariantYieldMessage, exception.Message);
    }

    [Fact]
    public void PlanVariant_SameParentLineTwice_ThrowsValidation()
    {
        var replacementId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1, [new(replacementId, Water, 380, false, 2, null, WaterLine, false)], []);
        var request = RequestFrom(Parent, "Variant") with
        {
            Ingredients = [new(WaterLine, Water, 350, SortOrder: 2), new(replacementId, Water, 380, SortOrder: 2)],
        };

        Assert.Throws<ValidationException>(() => RecipePlanner.PlanVariant(request, state, Parent, StageTypes));
    }

    [Fact]
    public void PlanVariant_SameParentStageTwice_ThrowsValidation()
    {
        var replacementId = Guid.NewGuid();
        var state = new RecipeState(Guid.NewGuid(), ParentId, 1, [],
            [new(replacementId, 1, "Mix", "mix", 10, 20, 0, null, null, false, true, false, null, MixStage, false)]);
        var request = RequestFrom(Parent, "Variant") with
        {
            Stages = [new(MixStage, "Mix", "mix", 10, 15, SortOrder: 1), new(replacementId, "Mix", "mix", 10, 20, SortOrder: 1)],
        };

        Assert.Throws<ValidationException>(() => RecipePlanner.PlanVariant(request, state, Parent, StageTypes));
    }

    [Fact]
    public void PlanNewVariant_InheritsEverything()
    {
        var plan = RecipePlanner.PlanNewVariant(" Rye version ", Parent);

        Assert.Equal("Rye version", plan.Header.Title);
        Assert.Equal(1, plan.Header.YieldLoaves);
        Assert.Empty(plan.Ingredients);
        Assert.Empty(plan.Stages);
    }

    private static RecipeState EmptyState() => new(Guid.NewGuid(), ParentId, 1, [], []);

    private static RecipeRequest RequestFrom(RecipeDto recipe, string title) => new(
        title, recipe.Description, recipe.YieldLoaves, recipe.LoafWeightG, recipe.Notes,
        recipe.Ingredients.Select(l => new RecipeIngredientInput(l.LineId, l.IngredientId, l.Quantity, l.IsOptional, l.Note, l.SortOrder)).ToList(),
        recipe.Stages.Select(s => new RecipeStageInput(s.LineId, s.Name, s.StageTypeCode, s.MinDurationMinutes, s.MaxDurationMinutes,
            s.ActiveMinutes, s.ProofMode, s.TemperatureC, s.UsesOven, s.UsesMixer, s.IsOptional, s.Instructions, s.SortOrder)).ToList());
}
