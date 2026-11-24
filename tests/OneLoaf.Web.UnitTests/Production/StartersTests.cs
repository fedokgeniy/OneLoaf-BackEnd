using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Production.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Production;

public sealed class StartersTests : BunitContext
{
    private readonly Api _api = new();

    public StartersTests() => this.AddProductionFakes(_api);

    [Fact]
    public void LogFeeding_ValidForm_SendsGramsAndReloadsTheStarter()
    {
        var cut = Render<StartersPage>();
        cut.WaitForAssertion(() => Assert.Contains("Mother rye", cut.Find(".starters__card").TextContent));
        var loadsBefore = _api.StarterLoads;

        cut.FindAll(".starters__actions button")[0].Click();
        cut.WaitForAssertion(() => cut.Find("form#feeding-form"));
        cut.FindAll("form#feeding-form input[inputmode=decimal]")[0].Change("50");
        cut.FindAll("form#feeding-form input[inputmode=decimal]")[1].Change("50");
        cut.FindAll("form#feeding-form input[inputmode=decimal]")[2].Change("20");
        cut.Find("form#feeding-form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_api.Feeding));
        Assert.Equal(50m, _api.Feeding!.FlourG);
        Assert.Equal(20m, _api.Feeding.StarterKeptG);
        cut.WaitForAssertion(() => Assert.True(_api.StarterLoads > loadsBefore));
    }

    [Fact]
    public void NewStarter_Save_SendsTheFormAndReturnsToTheList()
    {
        var cut = Render<StarterEditorPage>();
        cut.WaitForAssertion(() => cut.Find("form#starter-form"));

        cut.Find("input[placeholder='Mother rye']").Change("Wheat levain");
        cut.FindAll("form#starter-form input[inputmode=decimal]")[1].Change("6");
        cut.Find("form#starter-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Wheat levain", _api.Created?.Name));
        Assert.Equal(100m, _api.Created!.HydrationPercent);
        Assert.Equal(6m, _api.Created.RiseHours);
        Assert.Equal(0.667m, _api.Created.UsableShare);
        Assert.Equal("regular", _api.Created.FeedingMode);
        Assert.True(_api.Created.IsActive);
        Assert.EndsWith("/starters", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    private sealed class Api : ProductionApiStub
    {
        public int StarterLoads { get; private set; }

        public FeedingRequest? Feeding { get; private set; }

        public StarterRequest? Created { get; private set; }

        public override Task<ApiResult<Shared.Contracts.PagedResult<StarterDto>>> ListStartersAsync(CancellationToken ct = default)
        {
            StarterLoads++;
            return base.ListStartersAsync(ct);
        }

        public override Task<ApiResult<FeedingDto>> AddFeedingAsync(Guid starterId, FeedingRequest request, CancellationToken ct = default)
        {
            Feeding = request;
            return base.AddFeedingAsync(starterId, request, ct);
        }

        public override Task<ApiResult<StarterDto>> CreateStarterAsync(StarterRequest request, CancellationToken ct = default)
        {
            Created = request;
            return base.CreateStarterAsync(request, ct);
        }
    }
}
