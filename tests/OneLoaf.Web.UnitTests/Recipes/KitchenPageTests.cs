using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class KitchenPageTests : BunitContext
{
    private readonly Catalog _catalog = new();

    public KitchenPageTests() => this.AddRecipesFakes(new RecipesApiStub(), _catalog);

    [Fact]
    public void FirstVisit_NotSavedYet_ShowsDefaultsAndSavesTheForm()
    {
        var cut = Render<KitchenPage>();
        cut.WaitForAssertion(() => Assert.Contains("Not set up yet.", cut.Find(".ol-alert").TextContent));

        cut.FindAll("input[inputmode=decimal]")[0].Change("2.5");
        cut.Find("input[inputmode=numeric]").Change("4");
        cut.Find("form#kitchen-form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_catalog.Saved));
        Assert.Equal(2.5m, _catalog.Saved!.OvenPowerKw);
        Assert.Equal(4, _catalog.Saved.OvenCapacityLoaves);
        Assert.Equal(0m, _catalog.Saved.PackagingCostPerLoaf);
        Assert.Equal("EUR", _catalog.Saved.Currency);
        Assert.False(string.IsNullOrWhiteSpace(_catalog.Saved.TimeZone));
    }

    [Fact]
    public void Submit_LossAboveHundred_ShowsFieldErrorAndSendsNothing()
    {
        var cut = Render<KitchenPage>();
        cut.WaitForAssertion(() => cut.Find("form#kitchen-form"));

        var loss = cut.FindAll("input[inputmode=decimal]")[4];
        loss.Change("150");
        cut.Find("form#kitchen-form").Submit();

        Assert.Contains("Enter 0 to 100 %.", cut.Markup);
        Assert.Null(_catalog.Saved);
    }

    private sealed class Catalog : CatalogApiStub
    {
        public KitchenSettingsRequest? Saved { get; private set; }

        public override Task<ApiResult<KitchenSettingsDto>> GetKitchenAsync(CancellationToken ct = default) =>
            Task.FromResult(ApiResult<KitchenSettingsDto>.Fail(ApiError.FromStatus(404)));

        public override Task<ApiResult<KitchenSettingsDto>> SaveKitchenAsync(KitchenSettingsRequest request, CancellationToken ct = default)
        {
            Saved = request;
            return Task.FromResult(ApiResult<KitchenSettingsDto>.Ok(new KitchenSettingsDto(request.OvenPowerKw, request.OvenCapacityLoaves,
                request.MixerBowlCapacityKg, request.ElectricityTariffPerKwh, request.PackagingCostPerLoaf, request.LossRatePercent,
                request.LaborRatePerHour, request.Currency, request.TimeZone, DateTimeOffset.UnixEpoch)));
        }
    }
}
