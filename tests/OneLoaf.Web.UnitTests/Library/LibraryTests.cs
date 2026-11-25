using Bunit.TestDoubles;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Library;
using OneLoaf.Web.Features.Library.Pages;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Library;

public sealed class LibraryTests : BunitContext
{
    private static readonly Guid PublicationId = Guid.NewGuid();
    private static readonly Guid CopyId = Guid.NewGuid();
    private static readonly IngredientDto Own = new(Guid.NewGuid(), "Wheat flour T550", "flour", "g", null, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    private readonly Api _api = new();
    private readonly BunitAuthorizationContext _auth;

    public LibraryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<OneLoaf.Web.Shared.Device.DeviceContext>();
        Services.AddSingleton<ILibraryApi>(_api);
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly, typeof(LibraryFeature).Assembly));
        _auth = AddAuthorization();
        Render<Fluxor.Blazor.Web.StoreInitializer>();
    }

    [Fact]
    public void Detail_Anonymous_ShowsRecipeAndAsksToSignIn()
    {
        var cut = Render<LibraryDetailPage>(p => p.Add(x => x.Id, PublicationId));

        cut.WaitForAssertion(() => Assert.Contains("Country sourdough", cut.Find(".publication__title").TextContent));
        Assert.Contains("500 g", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Sign in to copy and comment", cut.Markup, StringComparison.Ordinal);
        Assert.All(cut.FindAll(".publication__reactions button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void React_SignedIn_SendsReactionAndShowsTheCountFromTheApi()
    {
        _auth.SetAuthorized("Boris");
        var cut = Render<LibraryDetailPage>(p => p.Add(x => x.Id, PublicationId));
        cut.WaitForAssertion(() => cut.Find(".publication__reactions button"));

        cut.FindAll(".publication__reactions button")[1].Click();

        cut.WaitForAssertion(() => Assert.Equal("love", _api.Reaction));
        cut.WaitForAssertion(() => Assert.Equal("true", cut.FindAll(".publication__reactions button")[1].GetAttribute("aria-pressed")));
        Assert.Contains("7", cut.FindAll(".publication__reactions button")[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_SignedIn_OpensTheNewRecipe()
    {
        _auth.SetAuthorized("Boris");
        var cut = Render<LibraryDetailPage>(p => p.Add(x => x.Id, PublicationId));
        cut.WaitForAssertion(() => cut.Find(".publication__title"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Save to my recipes", StringComparison.Ordinal)).Click();

        // The recipe names its flour "Bread flour"; the baker has "Wheat flour T550" and uses it instead of a duplicate.
        cut.WaitForAssertion(() => cut.Find(".copy__row select"));
        cut.Find(".copy__row select").Change(Own.Id.ToString());
        cut.FindAll("button").Single(b => b.TextContent.Contains("Save recipe", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.EndsWith($"/recipes/{CopyId}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal));
        Assert.Equal(new CopyIngredientChoice("Bread flour", Own.Id, null), Assert.Single(_api.Copied!.Ingredients));
    }

    [Fact]
    public void Moderation_ResolveWithoutReason_AsksForReasonAndSendsNothing()
    {
        _auth.SetAuthorized("Admin").SetRoles("admin");
        var cut = Render<ModerationPage>();
        cut.WaitForAssertion(() => cut.Find("tbody tr"));

        cut.FindAll("tbody button").First(b => b.TextContent.Contains("Reject", StringComparison.Ordinal)).Click();
        cut.Find("form#resolve-form").Submit();

        Assert.Contains("Give a reason", cut.Markup, StringComparison.Ordinal);
        Assert.Null(_api.Resolved);

        cut.Find("#resolve-reason").Input("Not a problem");
        cut.Find("form#resolve-form").Submit();
        cut.WaitForAssertion(() => Assert.Equal(LibraryCodes.Reject, _api.Resolved?.Action));
    }

    [Fact]
    public void LibraryPage_Search_SendsTheTermAndShowsMore()
    {
        var cut = Render<LibraryPage>();
        cut.WaitForAssertion(() => Assert.Contains("Country sourdough", cut.Find(".ol-card").TextContent));

        cut.Find("#library-search").Change("rye");
        cut.Find("form.library__search").Submit();
        cut.WaitForAssertion(() => Assert.Equal("rye", _api.LastSearch));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Show more recipes", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.Equal(2, _api.LastPage));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ol-card").Count));
    }

    [Fact]
    public void Publish_Confirmed_OpensThePublication()
    {
        _auth.SetAuthorized("Boris");
        var cut = Render<PublishPage>(p => p.Add(x => x.RecipeId, Guid.NewGuid()));
        cut.WaitForAssertion(() => Assert.Contains("will be visible to everyone", cut.Markup, StringComparison.Ordinal));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Publish").Click();

        cut.WaitForAssertion(() => Assert.EndsWith($"/library/{PublicationId}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal));
    }

    [Fact]
    public void CommentAndReport_SignedIn_PostsCommentAndSendsTheReason()
    {
        _auth.SetAuthorized("Boris");
        var cut = Render<LibraryDetailPage>(p => p.Add(x => x.Id, PublicationId));
        cut.WaitForAssertion(() => cut.Find("#comment-body"));

        cut.Find("form.publication__comment-form").Submit();
        Assert.Contains("Write a comment first.", cut.Markup, StringComparison.Ordinal);
        cut.Find("#comment-body").Input("Lovely crumb");
        cut.Find("form.publication__comment-form").Submit();
        cut.WaitForAssertion(() => Assert.Contains("Lovely crumb", cut.Find(".publication__comment-list").TextContent, StringComparison.Ordinal));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Report").Click();
        cut.Find("#report-reason").Input("Wrong amounts");
        cut.Find("form#report-form").Submit();
        cut.WaitForAssertion(() => Assert.Equal("Wrong amounts", _api.Reported?.Reason));
    }

    private sealed class Api : ILibraryApi
    {
        public string? Reaction { get; private set; }

        public string? LastSearch { get; private set; }

        public int LastPage { get; private set; }

        public ReportRequest? Reported { get; private set; }

        public ResolveReportRequest? Resolved { get; private set; }

        public Task<ApiResult<PagedResult<LibraryItemDto>>> SearchAsync(string? search, int page, CancellationToken ct = default)
        {
            LastSearch = search;
            LastPage = page;
            return Ok(new PagedResult<LibraryItemDto>(
                [new LibraryItemDto(Guid.NewGuid(), "Country sourdough", null, "Docs Baker", DateTimeOffset.UnixEpoch, 1, 4, 5, 0, 0)], page, 1, 21));
        }

        public Task<ApiResult<PublishedRecipeDto>> GetAsync(Guid id, CancellationToken ct = default) => Ok(new PublishedRecipeDto(
            id, "Country sourdough", "Open crumb.", "Docs Baker", "published", DateTimeOffset.UnixEpoch,
            new PublicationSnapshotDto([new SnapshotIngredientDto("Bread flour", "g", "flour", 500m, false)],
                [new SnapshotStageDto("Bake", null, "bake", false, 45, 40, 240m)], 900m, 1, 1),
            0, Reaction is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [Reaction] = 7 }, Reaction, false));

        public Task<ApiResult<PagedResult<CommentDto>>> ListCommentsAsync(Guid id, int page, CancellationToken ct = default) =>
            Ok(new PagedResult<CommentDto>([], 1, 20, 0));

        public Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string body, CancellationToken ct = default) =>
            Ok(new CommentDto(Guid.NewGuid(), "Boris", body, DateTimeOffset.UnixEpoch, null, true));

        public Task<ApiResult> RemoveCommentAsync(Guid id, Guid commentId, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<ApiResult> SetReactionAsync(Guid id, string? reaction, CancellationToken ct = default)
        {
            Reaction = reaction;
            return Task.FromResult(ApiResult.Ok);
        }

        public CopyPublicationRequest? Copied { get; private set; }

        public Task<ApiResult<RecipeDto>> CopyAsync(Guid id, CopyPublicationRequest request, CancellationToken ct = default)
        {
            Copied = request;
            return Ok(Recipe(CopyId));
        }

        public Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ApiResult<FileDownload>>(new ApiResult<FileDownload>.Failure(ApiError.Network(null)));

        /// <summary>The baker already has this flour, under another name.</summary>
        public Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default) =>
            Ok(new PagedResult<IngredientDto>([Own], 1, 100, 1));

        public Task<ApiResult<ReportCreatedResponse>> ReportAsync(Guid id, ReportRequest request, CancellationToken ct = default)
        {
            Reported = request;
            return Ok(new ReportCreatedResponse(Guid.NewGuid()));
        }

        public Task<ApiResult> UnpublishAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<ApiResult<PublishResponse>> PublishAsync(Guid recipeId, CancellationToken ct = default) => Ok(new PublishResponse(PublicationId));

        public Task<ApiResult<RecipeDto>> GetRecipeAsync(Guid recipeId, CancellationToken ct = default) =>
            Task.FromResult<ApiResult<RecipeDto>>(new ApiResult<RecipeDto>.Success(Recipe(recipeId)));

        public Task<ApiResult<PagedResult<ReportDto>>> ListReportsAsync(string status, int page, CancellationToken ct = default) =>
            Ok(new PagedResult<ReportDto>([new ReportDto(Guid.NewGuid(), PublicationId, "Country sourdough", null, null, "Wrong amounts", "open",
                DateTimeOffset.UnixEpoch, null)], 1, 20, 1));

        public Task<ApiResult> ResolveReportAsync(Guid reportId, ResolveReportRequest request, CancellationToken ct = default)
        {
            Resolved = request;
            return Task.FromResult(ApiResult.Ok);
        }

        private static RecipeDto Recipe(Guid id) =>
            new(id, null, "Country sourdough", null, 1, 900m, null, false, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        private static Task<ApiResult<T>> Ok<T>(T value) => Task.FromResult<ApiResult<T>>(new ApiResult<T>.Success(value));
    }
}
