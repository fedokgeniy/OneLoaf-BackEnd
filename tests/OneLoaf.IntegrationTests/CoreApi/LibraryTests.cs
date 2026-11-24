using System.Net;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Library;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Shared library (US-39 - US-42) and moderation (LR-11).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class LibraryTests(ApiFixture fixture)
{
    private static readonly Guid Admin = new("00000000-0000-4000-a001-0000000000a5");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task US39_US40_US42_PublishBrowseAnonymouslyCopyAndUnpublish()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var visitor = fixture.Api.CreateClient();

        var published = await (await anna.PostAsync($"/api/v1/recipes/{Seed.R1}/publish", null, Ct)).ReadAsync<PublishResponse>();
        var page = await (await visitor.GetAsync("/api/v1/library?search=white&pageSize=100", Ct)).ReadAsync<PagedResult<LibraryItemDto>>();
        var publication = await (await visitor.GetAsync($"/api/v1/library/{published.PublishedRecipeId}", Ct)).ReadAsync<PublishedRecipeDto>();
        using var copyResponse = await boris.PostAsync($"/api/v1/library/{published.PublishedRecipeId}/copy", null, Ct);
        Assert.True(copyResponse.StatusCode == HttpStatusCode.Created, await copyResponse.Content.ReadAsStringAsync(Ct));
        var copy = await copyResponse.ReadAsync<RecipeDto>();
        using var unpublish = await boris.PostAsync($"/api/v1/library/{published.PublishedRecipeId}/unpublish", null, Ct);
        using var annaUnpublish = await anna.PostAsync($"/api/v1/library/{published.PublishedRecipeId}/unpublish", null, Ct);
        using var hidden = await visitor.GetAsync($"/api/v1/library/{published.PublishedRecipeId}", Ct);
        var after = await (await visitor.GetAsync("/api/v1/library?search=white&pageSize=100", Ct)).ReadAsync<PagedResult<LibraryItemDto>>();

        Assert.Contains(page.Items, i => i.Id == published.PublishedRecipeId);
        Assert.Equal(publication.Snapshot.GetProperty("ingredients").GetArrayLength(), copy.Ingredients.Count);
        Assert.Equal(publication.Snapshot.GetProperty("stages").GetArrayLength(), copy.Stages.Count);
        Assert.Equal(HttpStatusCode.NotFound, unpublish.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, annaUnpublish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.DoesNotContain(after.Items, i => i.Id == published.PublishedRecipeId);
    }

    [Fact]
    public async Task US42_CopyWithChoices_UsesAnOwnIngredientInsteadOfADuplicateAndStocksANewOne()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var visitor = fixture.Api.CreateClient();
        var flour = await (await anna.PostAsJsonAsync("/api/v1/ingredients", new IngredientRequest("Copy test T550", "flour", "g", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        var seeds = await (await anna.PostAsJsonAsync("/api/v1/ingredients", new IngredientRequest("Copy test seeds", "add_in", "g", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        var recipe = await (await anna.PostAsJsonAsync("/api/v1/recipes", new RecipeRequest("Copy test loaf", null, 1, null, null,
            [new(null, flour.Id, 500), new(null, seeds.Id, 50)], [new(null, "Bake", "bake", 40, 45)]), CoreApiFactory.Json, Ct)).ReadAsync<RecipeDto>();

        // The photo of the recipe is the photo of its publication, readable without a token.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0];
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "loaf.png");
        using var uploaded = await anna.PutAsync($"/api/v1/recipes/{recipe.Id}/photo", form, Ct);
        Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);
        var published = await (await anna.PostAsync($"/api/v1/recipes/{recipe.Id}/publish", null, Ct)).ReadAsync<PublishResponse>();
        var publication = await (await visitor.GetAsync($"/api/v1/library/{published.PublishedRecipeId}", Ct)).ReadAsync<PublishedRecipeDto>();
        Assert.True(publication.HasPhoto);
        using var photo = await visitor.GetAsync($"/api/v1/library/{published.PublishedRecipeId}/photo", Ct);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);

        // Boris calls the same flour by another name: he uses his own, and says how many seeds he has.
        var own = await (await boris.PostAsJsonAsync("/api/v1/ingredients", new IngredientRequest("Boris strong flour", "flour", "g", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        using var copyResponse = await boris.PostAsJsonAsync($"/api/v1/library/{published.PublishedRecipeId}/copy",
            new CopyPublicationRequest([new("Copy test T550", own.Id), new("Copy test seeds", null, 200m)]), CoreApiFactory.Json, Ct);
        Assert.True(copyResponse.StatusCode == HttpStatusCode.Created, await copyResponse.Content.ReadAsStringAsync(Ct));
        var copy = await copyResponse.ReadAsync<RecipeDto>();

        Assert.Contains(copy.Ingredients, i => i.IngredientId == own.Id && i.Quantity == 500m);
        var mine = await (await boris.GetAsync("/api/v1/ingredients?search=Copy%20test&pageSize=100", Ct)).ReadAsync<PagedResult<IngredientDto>>();
        var added = Assert.Single(mine.Items);
        Assert.Equal("Copy test seeds", added.Name);
        Assert.Equal(200m, added.Stock);

        // Another unit would change the quantity of the recipe line.
        var litres = await (await boris.PostAsJsonAsync("/api/v1/ingredients", new IngredientRequest("Boris flour in kilograms", "flour", "kg", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        using var wrongUnit = await boris.PostAsJsonAsync($"/api/v1/library/{published.PublishedRecipeId}/copy",
            new CopyPublicationRequest([new("Copy test T550", litres.Id)]), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongUnit.StatusCode);
    }

    [Fact]
    public async Task US41_CommentAndReact_ShownWithAuthorAndOnlyAuthorRemovesComment()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var visitor = fixture.Api.CreateClient();
        var published = await (await anna.PostAsync($"/api/v1/recipes/{Seed.R1}/publish", null, Ct)).ReadAsync<PublishResponse>();
        var route = $"/api/v1/library/{published.PublishedRecipeId}";

        var comment = await (await boris.PostAsJsonAsync($"{route}/comments", new CommentRequest("Lovely crumb!"), CoreApiFactory.Json, Ct))
            .ReadAsync<CommentDto>();
        using var reaction = await boris.PutAsJsonAsync($"{route}/reactions/me", new ReactionRequest(ReactionType.WantToBake), CoreApiFactory.Json, Ct);
        var comments = await (await visitor.GetAsync($"{route}/comments", Ct)).ReadAsync<PagedResult<CommentDto>>();
        var seenByBoris = await (await boris.GetAsync(route, Ct)).ReadAsync<PublishedRecipeDto>();
        using var annaRemoves = await anna.DeleteAsync($"{route}/comments/{comment.Id}", Ct);
        using var borisRemoves = await boris.DeleteAsync($"{route}/comments/{comment.Id}", Ct);
        using var empty = await boris.PostAsJsonAsync($"{route}/comments", new CommentRequest("  "), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, reaction.StatusCode);
        Assert.Contains(comments.Items, c => c.Id == comment.Id && c.Body == "Lovely crumb!" && !c.IsMine && c.AuthorName.Length > 0);
        Assert.Equal(ReactionType.WantToBake, seenByBoris.MyReaction);
        Assert.Equal(1, seenByBoris.Reactions[ReactionType.WantToBake]);
        Assert.Equal(HttpStatusCode.NotFound, annaRemoves.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, borisRemoves.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task LR11_ReportedPublication_RemovedByAdminOnly()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var admin = fixture.CreateClientAs(Admin, UserRole.Admin);
        using var visitor = fixture.Api.CreateClient();
        var published = await (await anna.PostAsync($"/api/v1/recipes/{Seed.R1}/publish", null, Ct)).ReadAsync<PublishResponse>();

        var report = await (await boris.PostAsJsonAsync($"/api/v1/library/{published.PublishedRecipeId}/reports",
            new ReportRequest("Advertising in the description"), CoreApiFactory.Json, Ct)).ReadAsync<ReportCreatedResponse>();
        using var forbidden = await boris.GetAsync("/api/v1/admin/reports", Ct);
        var open = await (await admin.GetAsync("/api/v1/admin/reports?pageSize=100", Ct)).ReadAsync<PagedResult<ReportDto>>();
        var decision = new ResolveReportRequest(ModerationAction.Remove, "Advertising is not allowed");
        using var resolved = await admin.PostAsJsonAsync($"/api/v1/admin/reports/{report.ReportId}/resolve", decision, CoreApiFactory.Json, Ct);
        using var again = await admin.PostAsJsonAsync($"/api/v1/admin/reports/{report.ReportId}/resolve", decision, CoreApiFactory.Json, Ct);
        using var removed = await visitor.GetAsync($"/api/v1/library/{published.PublishedRecipeId}", Ct);
        var seenByAuthor = await (await anna.GetAsync($"/api/v1/library/{published.PublishedRecipeId}", Ct)).ReadAsync<PublishedRecipeDto>();

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains(open.Items, r => r.Id == report.ReportId && r.PublishedRecipeId == published.PublishedRecipeId);
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        Assert.Equal(PublicationStatus.Removed, seenByAuthor.Status);
    }
}
