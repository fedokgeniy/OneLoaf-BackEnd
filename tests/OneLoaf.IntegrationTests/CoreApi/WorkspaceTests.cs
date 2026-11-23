using System.Net;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Kitchen, catalog, prices, recipes and variants end to end, mostly in a fresh workspace.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class WorkspaceTests(ApiFixture fixture)
{
    [Fact]
    public async Task ReferenceData_IsPublic()
    {
        using var anonymous = fixture.Api.CreateClient();

        var units = await (await anonymous.GetAsync("/api/v1/reference/units", Ct)).ReadAsync<List<UnitDto>>();
        var stageTypes = await (await anonymous.GetAsync("/api/v1/reference/stage-types", Ct)).ReadAsync<List<StageTypeDto>>();

        Assert.Contains(units, u => u.Code == "g");
        Assert.NotEmpty(stageTypes);
    }

    [Fact]
    public async Task US13_KitchenSettings_DefaultsAfterRegistrationAndReplace()
    {
        using var client = await NewWorkspaceAsync();

        var defaults = await (await client.GetAsync("/api/v1/kitchen-settings", Ct)).ReadAsync<KitchenSettingsDto>();
        Assert.Equal("EUR", defaults.Currency);
        Assert.Null(defaults.OvenPowerKw);

        var request = new KitchenSettingsRequest(2.5m, 4, 4m, 0.25m, 0.15m, 10m, 12m, "EUR", "Europe/Vilnius");
        var updated = await (await client.PutAsJsonAsync("/api/v1/kitchen-settings", request, CoreApiFactory.Json, Ct))
            .ReadAsync<KitchenSettingsDto>();

        Assert.Equal(2.5m, updated.OvenPowerKw);
        Assert.Equal(10m, updated.LossRatePercent);
    }

    [Theory]
    [InlineData("XXX", "Europe/Vilnius", "currency")]
    [InlineData("EUR", "Mars/Olympus", "timeZone")]
    public async Task KitchenSettings_UnknownCurrencyOrTimeZone_Returns400(string currency, string timeZone, string field)
    {
        using var client = await NewWorkspaceAsync();

        using var response = await client.PutAsJsonAsync("/api/v1/kitchen-settings",
            new KitchenSettingsRequest(null, null, null, null, 0, 0, null, currency, timeZone), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task US10_US11_US12_CatalogWithPriceHistory()
    {
        using var client = await NewWorkspaceAsync();

        using var createdFlour = await client.PostAsJsonAsync("/api/v1/flours",
            new FlourRequest("Rye T1150", "rye", 9.5m, "medium", null), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, createdFlour.StatusCode);
        var flour = await createdFlour.ReadAsync<FlourDto>();

        using var duplicateFlour = await client.PostAsJsonAsync("/api/v1/flours",
            new FlourRequest("Rye T1150", "rye", null, null, null), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicateFlour.StatusCode);

        var ingredient = await CreateIngredientAsync(client, "Rye flour", "flour", "g", flour.Id);
        Assert.Equal(9.5m, ingredient.ProteinPercent);
        Assert.Null(ingredient.CurrentPrice);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await AddPriceAsync(client, ingredient.Id, 1.20m, today.AddDays(-30));
        await AddPriceAsync(client, ingredient.Id, 1.35m, today);

        using var duplicateDate = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.Id}/prices",
            new PriceRequest(1.40m, 1000, "EUR", today), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicateDate.StatusCode);

        var history = await (await client.GetAsync($"/api/v1/ingredients/{ingredient.Id}/prices", Ct)).ReadAsync<List<PriceDto>>();
        Assert.Equal([1.35m, 1.20m], history.Select(p => p.Price));
        Assert.Equal(12.5m, history[0].ChangePercent);

        var current = await (await client.GetAsync($"/api/v1/ingredients/{ingredient.Id}", Ct)).ReadAsync<IngredientDto>();
        Assert.Equal(1.35m, current.CurrentPrice?.Price);

        var list = await (await client.GetAsync("/api/v1/ingredients?search=rye", Ct)).ReadAsync<PagedResult<IngredientDto>>();
        Assert.Equal(1, list.TotalCount);
    }

    [Fact]
    public async Task Ingredient_UnknownUnit_Returns400()
    {
        using var client = await NewWorkspaceAsync();

        using var response = await client.PostAsJsonAsync("/api/v1/ingredients",
            new IngredientRequest("Salt", "other", "furlong", null), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task US05_US07_US08_RecipeVariantAndComparison()
    {
        using var client = await NewWorkspaceAsync();
        var flour = await CreateIngredientAsync(client, "Bread flour", "flour", "g", null);
        var water = await CreateIngredientAsync(client, "Water", "water", "g", null);
        var salt = await CreateIngredientAsync(client, "Salt", "salt", "g", null);

        var request = new RecipeRequest("Country loaf", "Basic", 1, 900, null,
            [new(null, flour.Id, 500), new(null, water.Id, 350), new(null, salt.Id, 10)],
            [new(null, "Mix", "mix", 10, 15, 10), new(null, "Bulk", "bulk_fermentation", 240, 300), new(null, "Bake", "bake", 40, 45, 5)]);
        using var created = await client.PostAsJsonAsync("/api/v1/recipes", request, CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recipe = await created.ReadAsync<RecipeDto>();
        Assert.Equal(3, recipe.Ingredients.Count);
        Assert.Equal(3, recipe.Stages.Count);
        Assert.Contains(recipe.Stages, s => s.StageTypeCode == "bake" && s.UsesOven);

        using var variantResponse = await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/variants",
            new CreateVariantRequest("Country loaf, more water"), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, variantResponse.StatusCode);
        var variant = await variantResponse.ReadAsync<RecipeDto>();
        Assert.All(variant.Ingredients, l => Assert.True(l.IsInherited));

        var changedWater = ToRequest(variant) with
        {
            Ingredients = variant.Ingredients.Select(l => new RecipeIngredientInput(l.LineId, l.IngredientId,
                l.IngredientId == water.Id ? 380 : l.Quantity, l.IsOptional, l.Note, l.SortOrder)).ToList(),
        };
        var updated = await (await client.PutAsJsonAsync($"/api/v1/recipes/{variant.Id}", changedWater, CoreApiFactory.Json, Ct))
            .ReadAsync<RecipeDto>();
        Assert.Equal(380m, updated.Ingredients.Single(l => l.IngredientId == water.Id).Quantity);
        Assert.Equal(2, updated.Ingredients.Count(l => l.IsInherited));

        var comparison = await (await client.GetAsync($"/api/v1/recipes/{variant.Id}/comparison", Ct)).ReadAsync<RecipeComparisonDto>();
        var changed = Assert.Single(comparison.Ingredients, c => c.Change == ChangeKind.Changed);
        Assert.Equal(350m, changed.Parent!.Quantity);
        Assert.Equal(380m, changed.Variant!.Quantity);

        // The parent keeps its line ids on update, so the variant still points at the right line.
        var parentUpdate = ToRequest(recipe) with { Title = "Country loaf v2" };
        using var parentUpdated = await client.PutAsJsonAsync($"/api/v1/recipes/{recipe.Id}", parentUpdate, CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, parentUpdated.StatusCode);
        var variantAfter = await (await client.GetAsync($"/api/v1/recipes/{variant.Id}", Ct)).ReadAsync<RecipeDto>();
        Assert.Equal(380m, variantAfter.Ingredients.Single(l => l.IngredientId == water.Id).Quantity);

        using var deleteParent = await client.DeleteAsync($"/api/v1/recipes/{recipe.Id}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleteParent.StatusCode);

        using var deleteVariant = await client.DeleteAsync($"/api/v1/recipes/{variant.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteVariant.StatusCode);

        using var deleteUsedIngredient = await client.DeleteAsync($"/api/v1/ingredients/{flour.Id}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleteUsedIngredient.StatusCode);
    }

    [Fact]
    public async Task Recipe_ArchivedIsHiddenFromTheDefaultList()
    {
        using var client = await NewWorkspaceAsync();
        using var created = await client.PostAsJsonAsync("/api/v1/recipes",
            new RecipeRequest("Old loaf", null, 1, null, null, [], []), CoreApiFactory.Json, Ct);
        var recipe = await created.ReadAsync<RecipeDto>();

        using var archive = await client.PostAsync($"/api/v1/recipes/{recipe.Id}/archive", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var active = await (await client.GetAsync("/api/v1/recipes", Ct)).ReadAsync<PagedResult<RecipeSummaryDto>>();
        var all = await (await client.GetAsync("/api/v1/recipes?includeArchived=true", Ct)).ReadAsync<PagedResult<RecipeSummaryDto>>();
        Assert.DoesNotContain(active.Items, r => r.Id == recipe.Id);
        Assert.Contains(all.Items, r => r.Id == recipe.Id);
    }

    [Fact]
    public async Task Recipe_ProofModeOnANonFermentationStage_Returns400()
    {
        using var client = await NewWorkspaceAsync();

        using var response = await client.PostAsJsonAsync("/api/v1/recipes",
            new RecipeRequest("Loaf", null, 1, null, null, [],
                [new(null, "Bake", "bake", 40, 45, ProofMode: OneLoaf.Domain.ProofMode.Cold)]), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task US07_SeedVariantOfVariant_ShowsInheritedLinesAndOverrides()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var r2 = await (await anna.GetAsync($"/api/v1/recipes/{Seed.R2}", Ct)).ReadAsync<RecipeDto>();
        var r3 = await (await anna.GetAsync($"/api/v1/recipes/{Seed.R3}", Ct)).ReadAsync<RecipeDto>();

        Assert.Equal(3, r2.Ingredients.Count(l => l.IsInherited));
        Assert.Equal(920m, r3.LoafWeightG);
        Assert.Equal("Low-sodium version for a customer", r3.Description);

        using var deleteR1 = await anna.DeleteAsync($"/api/v1/recipes/{Seed.R1}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleteR1.StatusCode);
    }

    [Fact]
    public async Task US37_FilterByFlourProtein()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var strong = await (await anna.GetAsync("/api/v1/recipes?minProteinPercent=12", Ct)).ReadAsync<PagedResult<RecipeSummaryDto>>();
        var tooStrong = await (await anna.GetAsync("/api/v1/recipes?minProteinPercent=20", Ct)).ReadAsync<PagedResult<RecipeSummaryDto>>();

        Assert.Contains(strong.Items, r => r.Id == Seed.R1);
        Assert.Empty(tooStrong.Items);
    }

    [Fact]
    public async Task Warnings_MissingPriceOfBoris()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        var warnings = await (await boris.GetAsync("/api/v1/warnings", Ct)).ReadAsync<List<WarningDto>>();

        Assert.Contains(warnings, w => w.IssueCode == "missing_price");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> NewWorkspaceAsync() =>
        await fixture.Api.CreateAuthenticatedClientAsync(await fixture.Api.RegisterAsync());

    private static async Task<IngredientDto> CreateIngredientAsync(HttpClient client, string name, string category, string unit, Guid? flourId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ingredients",
            new IngredientRequest(name, category, unit, flourId), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<IngredientDto>();
    }

    private static async Task AddPriceAsync(HttpClient client, Guid ingredientId, decimal price, DateOnly from)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredientId}/prices",
            new PriceRequest(price, 1000, "EUR", from), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static RecipeRequest ToRequest(RecipeDto recipe) => new(
        recipe.Title, recipe.Description, recipe.YieldLoaves, recipe.LoafWeightG, recipe.Notes,
        recipe.Ingredients.Select(l => new RecipeIngredientInput(l.LineId, l.IngredientId, l.Quantity, l.IsOptional, l.Note, l.SortOrder)).ToList(),
        recipe.Stages.Select(s => new RecipeStageInput(s.LineId, s.Name, s.StageTypeCode, s.MinDurationMinutes, s.MaxDurationMinutes,
            s.ActiveMinutes, s.ProofMode, s.TemperatureC, s.UsesOven, s.UsesMixer, s.IsOptional, s.Instructions, s.SortOrder)).ToList());
}
