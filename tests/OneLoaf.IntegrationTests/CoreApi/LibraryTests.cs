using System.Net;
using System.Net.Http.Json;
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
