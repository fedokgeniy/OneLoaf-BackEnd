using System.Net;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>US-03: user B (Boris) never sees, changes or references user A's (Anna's) data; answers are 404.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class IsolationTests(ApiFixture fixture)
{
    public static TheoryData<string, string> ForeignResources => new()
    {
        { "GET", $"/api/v1/recipes/{Seed.R1}" },
        { "PUT", $"/api/v1/recipes/{Seed.R1}" },
        { "DELETE", $"/api/v1/recipes/{Seed.R1}" },
        { "POST", $"/api/v1/recipes/{Seed.R1}/archive" },
        { "POST", $"/api/v1/recipes/{Seed.R1}/variants" },
        { "GET", $"/api/v1/recipes/{Seed.R2}/comparison" },
        { "GET", $"/api/v1/recipes/{Seed.R1}/scaled?loaves=2" },
        { "GET", $"/api/v1/recipes/{Seed.R1}/cost?loaves=1" },
        { "GET", $"/api/v1/recipes/{Seed.R1}/timeline?start=2026-11-20T12:00:00Z" },
        { "GET", $"/api/v1/recipes/{Seed.R1}/start-window?readyAt=2026-11-20T08:00:00Z" },
        { "GET", $"/api/v1/recipes/{Seed.R1}/production-figures?loaves=1" },
        { "GET", $"/api/v1/flours/{Seed.AnnaFlour}" },
        { "PUT", $"/api/v1/flours/{Seed.AnnaFlour}" },
        { "DELETE", $"/api/v1/flours/{Seed.AnnaFlour}" },
        { "GET", $"/api/v1/ingredients/{Seed.AnnaBreadFlourIngredient}" },
        { "PUT", $"/api/v1/ingredients/{Seed.AnnaBreadFlourIngredient}" },
        { "DELETE", $"/api/v1/ingredients/{Seed.AnnaBreadFlourIngredient}" },
        { "GET", $"/api/v1/ingredients/{Seed.AnnaBreadFlourIngredient}/prices" },
        { "POST", $"/api/v1/ingredients/{Seed.AnnaBreadFlourIngredient}/prices" },
    };

    [Theory]
    [MemberData(nameof(ForeignResources))]
    public async Task US03_OtherUsersResource_Returns404(string method, string url)
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = ValidBodyFor(method, url) };

        using var response = await boris.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("White homemade loaf", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task US03_ListsNeverContainOtherUsersRows()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        var recipes = await (await boris.GetAsync("/api/v1/recipes?includeArchived=true&pageSize=100", Ct)).ReadAsync<PagedResult<RecipeSummaryDto>>();
        var flours = await (await boris.GetAsync("/api/v1/flours?pageSize=100", Ct)).ReadAsync<PagedResult<FlourDto>>();
        var ingredients = await (await boris.GetAsync("/api/v1/ingredients?pageSize=100", Ct)).ReadAsync<PagedResult<IngredientDto>>();

        Assert.Contains(recipes.Items, r => r.Id == Seed.R5);
        Assert.DoesNotContain(recipes.Items, r => r.Id == Seed.R1 || r.Id == Seed.R2 || r.Id == Seed.R3);
        Assert.DoesNotContain(flours.Items, f => f.Id == Seed.AnnaFlour);
        Assert.DoesNotContain(ingredients.Items, i => i.Id == Seed.AnnaBreadFlourIngredient);
    }

    [Fact]
    public async Task US03_CannotUseOtherUsersIngredientInOwnRecipe()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        using var response = await boris.PostAsJsonAsync("/api/v1/recipes",
            new RecipeRequest("Stolen loaf", null, 1, null, null, [new(null, Seed.AnnaBreadFlourIngredient, 500)], []),
            CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task US03_CannotLinkOtherUsersFlour()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        using var response = await boris.PostAsJsonAsync("/api/v1/ingredients",
            new IngredientRequest("Borrowed flour", "flour", "g", Seed.AnnaFlour), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A valid body, so the request passes validation and reaches the ownership check.</summary>
    private static JsonContent? ValidBodyFor(string method, string url)
    {
        object? body = (method, url) switch
        {
            ("PUT", var u) when u.Contains("/recipes/", StringComparison.Ordinal) =>
                new RecipeRequest("Taken over", null, 1, null, null, [], []),
            ("POST", var u) when u.EndsWith("/variants", StringComparison.Ordinal) => new CreateVariantRequest("Copy"),
            ("PUT", var u) when u.Contains("/flours/", StringComparison.Ordinal) => new FlourRequest("Taken over", "wheat", null, null, null),
            ("PUT", var u) when u.Contains("/ingredients/", StringComparison.Ordinal) => new IngredientRequest("Taken over", "flour", "g", null),
            ("POST", var u) when u.EndsWith("/prices", StringComparison.Ordinal) =>
                new PriceRequest(1, 1000, "EUR", new DateOnly(2030, 1, 1)),
            _ => null,
        };
        return body is null ? null : JsonContent.Create(body, body.GetType(), options: CoreApiFactory.Json);
    }
}
