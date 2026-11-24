using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class RecipeEditorPageTests : BunitContext
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly IngredientDto Flour = Ingredient("Bread flour");
    private static readonly IngredientDto Water = Ingredient("Water");
    private readonly EditorApi _api = new();

    public RecipeEditorPageTests()
    {
        this.AddRecipesFakes(_api, new EditorCatalog());
    }

    [Fact]
    public void Save_ServerRejectsStage_ShowsMessageUnderThatStageField()
    {
        _api.UpdateResult = ApiResult<RecipeDto>.Fail(ApiError.FromProblem(new ProblemDetailsDto(
            "https://oneloaf.app/problems/validation", "One or more validation errors occurred.", 400, null, null,
            new Dictionary<string, string[]> { ["stages[1].MaxDurationMinutes"] = ["Must be at least 240."] }, null, "c-7"), 400, null, null));
        var cut = Render<RecipeEditorPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => cut.Find("form#recipe-editor"));

        cut.Find("form#recipe-editor").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Must be at least 240.", cut.FindAll("li.line--stage")[1].TextContent));
        Assert.DoesNotContain("Must be at least 240.", cut.FindAll("li.line--stage")[0].TextContent);
        Assert.NotNull(_api.LastRequest);
    }

    [Fact]
    public void MoveDown_FirstIngredient_ChangesTheOrderSent()
    {
        var cut = Render<RecipeEditorPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => cut.Find("form#recipe-editor"));

        cut.Find("button[aria-label='Move Bread flour down']").Click();
        cut.Find("form#recipe-editor").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_api.LastRequest));
        Assert.Equal([Water.Id, Flour.Id], _api.LastRequest!.Ingredients.Select(i => i.IngredientId));
        Assert.Equal([1, 2], _api.LastRequest.Ingredients.Select(i => i.SortOrder!.Value));
        Assert.EndsWith($"recipes/{Id}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyStage_SecondStage_AddsANewStageWithTheSameSettingsAfterIt()
    {
        var cut = Render<RecipeEditorPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => cut.Find("form#recipe-editor"));

        cut.Find("button[aria-label='Copy Bulk']").Click();
        cut.Find("form#recipe-editor").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_api.LastRequest));
        var stages = _api.LastRequest!.Stages;
        Assert.Equal(["Mix", "Bulk", "Bulk"], stages.Select(s => s.Name));
        Assert.NotNull(stages[1].LineId);
        Assert.Null(stages[2].LineId);
        Assert.Equal(stages[1].MaxDurationMinutes, stages[2].MaxDurationMinutes);
    }

    [Fact]
    public void NewIngredient_FromTheEditor_IsCreatedAndGetsALineOfItsOwn()
    {
        var cut = Render<RecipeEditorPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => cut.Find("form#recipe-editor"));

        cut.FindAll("form#recipe-editor button").Single(b => b.TextContent.Contains("New ingredient", StringComparison.Ordinal)).Click();
        cut.Find("#editor-ingredient-form input").Change("Sea salt");
        cut.Find("#editor-ingredient-form select").Change("water");
        cut.Find("#editor-ingredient-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("li.line:not(.line--stage)").Count));
        Assert.Equal("Sea salt", EditorCatalog.Created?.Name);
        Assert.Empty(cut.FindAll("#editor-ingredient-form"));
        Assert.Equal(EditorCatalog.CreatedId.ToString(), cut.FindAll("li.line:not(.line--stage)")[2].QuerySelector("select")!.GetAttribute("value"));
    }

    [Fact]
    public void Submit_EmptyNewRecipe_ShowsRowErrorsAndSendsNothing()
    {
        var cut = Render<RecipeEditorPage>();
        cut.WaitForAssertion(() => cut.Find("form#recipe-editor"));

        cut.Find("form#recipe-editor").Submit();

        var errors = cut.FindAll(".ol-field__error").Select(e => e.TextContent).ToList();
        Assert.Contains(errors, e => e.Contains("Enter a title.", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Choose an ingredient.", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Enter a stage name.", StringComparison.Ordinal));
        Assert.Null(_api.LastRequest);
        Assert.Contains("Some fields need attention.", cut.Find(".ol-alert").TextContent);
    }

    private static IngredientDto Ingredient(string name) =>
        new(Guid.NewGuid(), name, "flour", "g", null, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static RecipeStageDto Stage(int order, string name, int min, int max) =>
        new(Guid.NewGuid(), order, name, "bulk_fermentation", min, max, 0, null, null, false, false, false, null, false);

    private sealed class EditorApi : RecipesApiStub
    {
        public RecipeRequest? LastRequest { get; private set; }

        public ApiResult<RecipeDto>? UpdateResult { get; set; }

        private static RecipeDto Recipe => new(Id, null, "Country sourdough", null, 1, 900m, null, false,
            [
                new RecipeIngredientDto(Guid.NewGuid(), Flour.Id, "Bread flour", "flour", "g", 500m, false, 1, null, null, false),
                new RecipeIngredientDto(Guid.NewGuid(), Water.Id, "Water", "water", "g", 350m, false, 2, null, null, false),
            ],
            [Stage(1, "Mix", 15, 20), Stage(2, "Bulk", 240, 300)],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        public override Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<RecipeDto>.Ok(Recipe));

        public override Task<ApiResult<RecipeDto>> UpdateAsync(Guid id, RecipeRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(UpdateResult ?? ApiResult<RecipeDto>.Ok(Recipe));
        }

        public override Task<ApiResult<RecipeDto>> CreateAsync(RecipeRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(ApiResult<RecipeDto>.Ok(Recipe));
        }
    }

    private sealed class EditorCatalog : CatalogApiStub
    {
        public static readonly Guid CreatedId = Guid.NewGuid();

        public static IngredientRequest? Created { get; private set; }

        public override Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<PagedResult<IngredientDto>>.Ok(new PagedResult<IngredientDto>([Flour, Water], 1, 100, 2)));

        public override Task<ApiResult<IngredientDto>> CreateIngredientAsync(IngredientRequest request, CancellationToken ct = default)
        {
            Created = request;
            return Task.FromResult(ApiResult<IngredientDto>.Ok(new IngredientDto(CreatedId, request.Name, request.CategoryCode, request.UnitCode,
                null, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
        }
    }
}
