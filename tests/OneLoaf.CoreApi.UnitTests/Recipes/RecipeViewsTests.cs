using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.UnitTests.Fakes;

namespace OneLoaf.CoreApi.UnitTests.Recipes;

public sealed class RecipeViewsTests
{
    private static readonly Guid Flour = Guid.NewGuid();
    private static readonly Guid Water = Guid.NewGuid();
    private static readonly Guid Salt = Guid.NewGuid();
    private static readonly Guid Seeds = Guid.NewGuid();

    [Fact]
    public void US06_Scale_OneLoafRecipeTo3Loaves_Flour1500()
    {
        var recipe = FakeRecipeRepository.Recipe(Guid.NewGuid(), null, "Loaf", 1,
            [Line(Guid.NewGuid(), Flour, 500, 1), Line(Guid.NewGuid(), Water, 350, 2)]);

        var scaled = RecipeViews.Scale(recipe, 3);

        Assert.Equal(3, scaled.Loaves);
        Assert.Equal([1500m, 1050m], scaled.Ingredients.Select(i => i.Quantity));
    }

    [Fact]
    public void Scale_RecipeForTwoLoavesToOne_HalvesAndRounds()
    {
        var recipe = FakeRecipeRepository.Recipe(Guid.NewGuid(), null, "Loaf", 3, [Line(Guid.NewGuid(), Salt, 10, 1)]);

        var scaled = RecipeViews.Scale(recipe, 1);

        Assert.Equal(3.333m, scaled.Ingredients[0].Quantity);
    }

    [Fact]
    public void Scale_ZeroLoaves_Throws()
    {
        var recipe = FakeRecipeRepository.Recipe(Guid.NewGuid(), null, "Loaf", 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => RecipeViews.Scale(recipe, 0));
    }

    [Fact]
    public void US08_Compare_ClassifiesEveryLine()
    {
        var parentId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var flourLine = Guid.NewGuid();
        var waterLine = Guid.NewGuid();
        var saltLine = Guid.NewGuid();
        var waterReplacement = Guid.NewGuid();
        var seedsLine = Guid.NewGuid();
        var mixStage = Guid.NewGuid();
        var bakeStage = Guid.NewGuid();
        var bakeReplacement = Guid.NewGuid();
        var coldProof = Guid.NewGuid();

        var parent = FakeRecipeRepository.Recipe(parentId, null, "Loaf", 1,
            [Line(flourLine, Flour, 500, 1), Line(waterLine, Water, 350, 2), Line(saltLine, Salt, 10, 3)],
            [Stage(mixStage, 1, false), Stage(bakeStage, 2, false)], loafWeightG: 900);
        var variant = FakeRecipeRepository.Recipe(variantId, parentId, "Loaf with seeds", 1,
            [Line(flourLine, Flour, 500, 1, inherited: true), Line(waterReplacement, Water, 380, 2), Line(seedsLine, Seeds, 50, 4)],
            [Stage(mixStage, 1, true), Stage(bakeReplacement, 2, false), Stage(coldProof, 3, false)], loafWeightG: 920);
        var state = new RecipeState(variantId, parentId, 1,
        [
            new(waterReplacement, Water, 380, false, 2, null, waterLine, false),
            new(Guid.NewGuid(), Salt, 10, false, 3, null, saltLine, true),
            new(seedsLine, Seeds, 50, false, 4, null, null, false),
        ],
        [
            new(bakeReplacement, 2, "Bake", "bake", 45, 50, 0, null, null, true, false, false, null, bakeStage, false),
            new(coldProof, 3, "Cold proof", "proof", 600, 720, 0, null, null, false, false, false, null, null, false),
        ]);

        var comparison = RecipeViews.Compare(variant, state, parent);

        Assert.Equal(["title", RecipePlanner.LoafWeightKey], comparison.ChangedFields);
        Assert.Equal(
            [ChangeKind.Unchanged, ChangeKind.Changed, ChangeKind.Removed, ChangeKind.Added],
            comparison.Ingredients.Select(c => c.Change));
        Assert.Equal(380m, comparison.Ingredients[1].Variant!.Quantity);
        Assert.Null(comparison.Ingredients[2].Variant);
        Assert.Null(comparison.Ingredients[3].Parent);
        Assert.Equal([ChangeKind.Unchanged, ChangeKind.Changed, ChangeKind.Added], comparison.Stages.Select(c => c.Change));
    }

    [Fact]
    public void Compare_RemovedStageAndChangedNotes()
    {
        var parentId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var mix = Guid.NewGuid();
        var parent = FakeRecipeRepository.Recipe(parentId, null, "Loaf", 1, [], [Stage(mix, 1, false)], description: "A", notes: "N");
        var variant = FakeRecipeRepository.Recipe(variantId, parentId, "Loaf", 1, [], [], description: "B", notes: "M");
        var state = new RecipeState(variantId, parentId, 1, [],
            [new(Guid.NewGuid(), 1, "Mix", "mix", 1, 2, 0, null, null, false, true, false, null, mix, true)]);

        var comparison = RecipeViews.Compare(variant, state, parent);

        Assert.Equal([RecipePlanner.DescriptionKey, RecipePlanner.NotesKey], comparison.ChangedFields);
        Assert.Equal(ChangeKind.Removed, Assert.Single(comparison.Stages).Change);
    }

    private static RecipeIngredientDto Line(Guid id, Guid ingredient, decimal quantity, int sort, bool inherited = false) =>
        new(id, ingredient, "x", "flour", "g", quantity, false, sort, null, null, inherited);

    private static RecipeStageDto Stage(Guid id, int sort, bool inherited) =>
        new(id, sort, "Stage", "mix", 1, 2, 0, null, null, false, true, false, null, inherited);
}
