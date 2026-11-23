using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Features.Production.Pages;
using OneLoaf.Web.Features.Production.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Production;

public sealed class NewBatchFormTests : BunitContext
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
    private readonly Api _api = new();

    public NewBatchFormTests() => this.AddProductionFakes(_api, _time);

    [Fact]
    public void Submit_WithoutRecipe_ShowsFieldErrorAndSendsNothing()
    {
        var cut = Render<NewBatchPage>();
        cut.WaitForAssertion(() => cut.Find("form#batch-form"));

        cut.Find("form#batch-form").Submit();

        Assert.Contains("Choose a recipe.", cut.Markup);
        Assert.Contains("Enter a name.", cut.Markup);
        Assert.Null(_api.Created);
    }

    [Fact]
    public void ChangeRecipe_PreviewsAfterTheDelayAndShowsApiFigures()
    {
        var cut = Render<NewBatchPage>();
        cut.WaitForAssertion(() => cut.Find("form#batch-form"));

        cut.FindAll("select")[0].Change(ProductionApiStub.Country.Id.ToString());
        Assert.Equal(0, _api.Previews);
        _time.Advance(NewBatchEffects.PreviewDelay);

        cut.WaitForAssertion(() => Assert.Contains("8.94 EUR", cut.Find(".new-batch__preview").TextContent));
        Assert.Equal(1, _api.Previews);
    }

    [Fact]
    public void Save_ServerRejectsLoaves_ShowsMessageUnderTheItem()
    {
        _api.CreateError = ApiError.FromProblem(new ProblemDetailsDto("https://oneloaf.app/problems/validation", "Invalid", 400, null, null,
            new Dictionary<string, string[]> { ["items[0].loaves"] = ["Too many loaves for the oven."] }, null, null), 400, null, null);
        var cut = Render<NewBatchPage>();
        cut.WaitForAssertion(() => cut.Find("form#batch-form"));
        cut.Find("input[placeholder='Saturday market']").Change("Saturday market");
        cut.FindAll("select")[0].Change(ProductionApiStub.Country.Id.ToString());

        cut.Find("form#batch-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Too many loaves for the oven.", cut.Find(".new-batch__item").TextContent));
        Assert.Equal("Saturday market", _api.Created!.Name);
        Assert.Equal(1, _api.Created.Items[0].Loaves);
    }

    [Fact]
    public void ReadyBy_TwoRecipes_PlansAtTheLatestCommonStart()
    {
        var cut = Render<NewBatchPage>();
        cut.WaitForAssertion(() => cut.Find("form#batch-form"));
        cut.FindAll("select")[0].Change(ProductionApiStub.Country.Id.ToString());

        cut.FindAll(".ol-segmented__opt")[1].Click();
        _time.Advance(NewBatchEffects.PreviewDelay);

        cut.WaitForAssertion(() => Assert.Contains("Possible start", cut.Find(".new-batch__preview").TextContent));
        // The stub's window for the ready-by time is [readyAt − 18 h, readyAt − 15 h]: the latest start is readyAt − 15 h.
        Assert.Equal(_api.LastReadyAt!.Value.AddHours(-15), _api.LastPreview!.PlannedStartAt);
    }

    private sealed class Api : ProductionApiStub
    {
        public int Previews { get; private set; }

        public BatchRequest? LastPreview { get; private set; }

        public DateTimeOffset? LastReadyAt { get; private set; }

        public BatchRequest? Created { get; private set; }

        public ApiError? CreateError { get; set; }

        public override Task<ApiResult<BatchDto>> PreviewAsync(BatchRequest request, CancellationToken ct = default)
        {
            Previews++;
            LastPreview = request;
            return base.PreviewAsync(request, ct);
        }

        public override Task<ApiResult<StartWindowDto>> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken ct = default)
        {
            LastReadyAt = readyAt;
            return base.GetStartWindowAsync(recipeId, readyAt, ct);
        }

        public override Task<ApiResult<BatchDto>> CreateBatchAsync(BatchRequest request, CancellationToken ct = default)
        {
            Created = request;
            return CreateError is null ? base.CreateBatchAsync(request, ct) : Task.FromResult(ApiResult<BatchDto>.Fail(CreateError));
        }
    }
}
