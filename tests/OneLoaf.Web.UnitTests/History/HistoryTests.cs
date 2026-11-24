using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.History;
using OneLoaf.Web.Features.History.Pages;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.History;

public sealed class HistoryTests : BunitContext
{
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly Guid RecipeId = Guid.NewGuid();
    private readonly Api _api = new();

    public HistoryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<OneLoaf.Web.Shared.Device.DeviceContext>();
        Services.AddSingleton<IHistoryApi>(_api);
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly, typeof(HistoryFeature).Assembly));
        Render<Fluxor.Blazor.Web.StoreInitializer>();
    }

    [Fact]
    public void HistoryPage_RatingChip_ReloadsWithMinimumRating()
    {
        var cut = Render<HistoryPage>();
        cut.WaitForAssertion(() => Assert.Contains("White loaf", cut.Find(".history__card").TextContent));
        Assert.Contains("Rated 4/5", cut.Find(".history__card").TextContent);

        cut.FindAll(".history__chips button").Single(b => b.TextContent.Contains("Rating 4+", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(4, _api.LastQuery?.MinRating));
        Assert.Contains("Clear filters", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveFeedback_RatingAndComment_SendsThePatch()
    {
        var cut = Render<BakeRecordPage>(p => p.Add(x => x.Id, RecordId));
        cut.WaitForAssertion(() => cut.Find("textarea"));

        cut.FindAll(".ol-rating input")[3].Change("4");
        cut.Find("textarea").Change("  Good oven spring  ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_api.Rated));
        Assert.Equal(4, _api.Rated!.Rating);
        Assert.Equal("Good oven spring", _api.Rated.Comment);
    }

    [Fact]
    public void Analyse_WithoutFeedback_AsksForFeedbackAndFocusesTheComment()
    {
        _api.AnalysisResult = new ApiResult<AnalysisDto>.Failure(ApiError.FromStatus(422) with { Detail = "Add a rating or a comment before asking for an analysis." });
        var cut = Render<BakeRecordPage>(p => p.Add(x => x.Id, RecordId));
        cut.WaitForAssertion(() => cut.Find("textarea"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Ask the assistant", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("before asking for an analysis", cut.Markup, StringComparison.Ordinal));
        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Analyse_Success_ShowsAnalysisAndUnverifiedNumbers()
    {
        _api.AnalysisResult = new ApiResult<AnalysisDto>.Success(new AnalysisDto(RecordId, "Bulk ran long.", ["27 °C"]));
        var cut = Render<BakeRecordPage>(p => p.Add(x => x.Id, RecordId));
        cut.WaitForAssertion(() => cut.Find("textarea"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Ask the assistant", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("Bulk ran long.", cut.Find(".bake__analysis-text").TextContent));
        Assert.Contains("27 °C", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Ask the assistant again", cut.Markup, StringComparison.Ordinal);
    }

    private sealed class Api : IHistoryApi
    {
        private static readonly BakeRecordDto Record = new(RecordId, null, new DateOnly(2026, 11, 21), 3, 6.05m, "EUR", null, null, null, null,
            [new BakeRecordItemDto(Guid.NewGuid(), RecipeId, "White loaf", 3, 6.05m)], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, []);

        public HistoryQuery? LastQuery { get; private set; }

        public UpdateBakeRecordRequest? Rated { get; private set; }

        public ApiResult<AnalysisDto> AnalysisResult { get; set; } = new ApiResult<AnalysisDto>.Failure(ApiError.FromStatus(503));

        public Task<ApiResult<PagedResult<BakeRecordSummaryDto>>> ListAsync(HistoryQuery query, int page, CancellationToken ct = default)
        {
            LastQuery = query;
            return Ok(new PagedResult<BakeRecordSummaryDto>(
                [new BakeRecordSummaryDto(RecordId, null, new DateOnly(2026, 11, 21), 3, 6.05m, "EUR", 4, false, ["White loaf"], 0)], 1, 20, 1));
        }

        public Task<ApiResult<IReadOnlyList<BakeStatisticsDto>>> GetStatisticsAsync(CancellationToken ct = default) =>
            Ok<IReadOnlyList<BakeStatisticsDto>>([new BakeStatisticsDto(RecipeId, "White loaf", 1, 3, 4m, 1, 2.02m, new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 21))]);

        public Task<ApiResult<BakeRecordDto>> GetAsync(Guid id, CancellationToken ct = default) => Ok(Record);

        public Task<ApiResult<BakeRecordDto>> RateAsync(Guid id, UpdateBakeRecordRequest request, CancellationToken ct = default)
        {
            Rated = request;
            return Ok(Record with { Rating = request.Rating, Comment = request.Comment });
        }

        public Task<ApiResult<IReadOnlyList<StageLogEntryDto>>> GetStageLogAsync(Guid id, CancellationToken ct = default) => Ok<IReadOnlyList<StageLogEntryDto>>([]);

        public Task<ApiResult<FileDownload>> GetPhotoAsync(string url, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ApiResult<BakeRecordPhotoDto>> UploadPhotoAsync(Guid id, Stream file, string fileName, string contentType, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ApiResult> DeletePhotoAsync(Guid id, Guid photoId, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<ApiResult<AnalysisDto>> AnalyseAsync(Guid id, CancellationToken ct = default) => Task.FromResult(AnalysisResult);

        private static Task<ApiResult<T>> Ok<T>(T value) => Task.FromResult<ApiResult<T>>(new ApiResult<T>.Success(value));
    }
}
