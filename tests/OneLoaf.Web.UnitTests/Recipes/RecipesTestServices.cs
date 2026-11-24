using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Device;
using OneLoaf.Web.Shared.Reference;

namespace OneLoaf.Web.UnitTests.Recipes;

/// <summary>
/// Everything the Recipes feature's Fluxor effects need, with fakes for the APIs: Fluxor creates every effect of a
/// scanned assembly, so a page test needs all their dependencies, not only those of the page under test.
/// </summary>
internal static class RecipesTestServices
{
    public static void AddRecipesFakes(
        this BunitContext context, IRecipesApi recipes, ICatalogApi? catalog = null, IReferenceApi? reference = null)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var services = context.Services;
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DeviceContext>();
        services.AddSingleton(recipes);
        services.AddSingleton(catalog ?? new CatalogApiStub());
        services.AddSingleton(reference ?? new ReferenceApiStub());
        services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly, typeof(RecipesFeature).Assembly));
        context.Render<Fluxor.Blazor.Web.StoreInitializer>();
    }
}

/// <summary>Fake catalog API; overridable per test.</summary>
internal class CatalogApiStub : ICatalogApi
{
    public virtual Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(ApiResult<PagedResult<IngredientDto>>.Ok(new PagedResult<IngredientDto>([], 1, pageSize, 0)));

    public virtual Task<ApiResult<IngredientDto>> CreateIngredientAsync(IngredientRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<IngredientDto>> UpdateIngredientAsync(Guid id, IngredientRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult> DeleteIngredientAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<PriceDto>> AddPriceAsync(Guid id, PriceRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<PriceDto>> UpdatePriceAsync(Guid id, Guid priceId, PriceRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult> DeletePriceAsync(Guid id, Guid priceId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<PagedResult<StockMovementDto>>> ListMovementsAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ApiResult<PagedResult<StockMovementDto>>.Ok(new PagedResult<StockMovementDto>([], 1, 100, 0)));

    public virtual Task<ApiResult<IngredientDto>> AddRecordAsync(Guid id, IngredientRecordRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<FileDownload>> GetIngredientPhotoAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ApiResult<FileDownload>.Fail(ApiError.FromStatus(404)));

    public virtual Task<ApiResult> SetIngredientPhotoAsync(Guid id, OneLoaf.Web.Shared.Ui.PhotoFile photo, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult> DeleteIngredientPhotoAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<StockMovementDto>> AdjustStockAsync(Guid id, StockAdjustmentRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<PagedResult<FlourDto>>.Ok(new PagedResult<FlourDto>([], 1, 100, 0)));

    public virtual Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<FlourDto>> UpdateFlourAsync(Guid id, FlourRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult> DeleteFlourAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<IReadOnlyList<WarningDto>>> GetWarningsAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<WarningDto>>.Ok([]));

    public virtual Task<ApiResult<KitchenSettingsDto>> GetKitchenAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<KitchenSettingsDto>> SaveKitchenAsync(KitchenSettingsRequest request, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>Fake reference API with a small fixed set.</summary>
internal sealed class ReferenceApiStub : IReferenceApi
{
    public Task<ApiResult<IReadOnlyList<StageTypeDto>>> GetStageTypesAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<StageTypeDto>>.Ok([new StageTypeDto("bulk_fermentation", "Bulk fermentation", true, false, false)]));

    public Task<ApiResult<IReadOnlyList<UnitDto>>> GetUnitsAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<UnitDto>>.Ok([new UnitDto("g", "gram", "mass", 1m), new UnitDto("kg", "kilogram", "mass", 1000m)]));

    public Task<ApiResult<IReadOnlyList<CurrencyDto>>> GetCurrenciesAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<CurrencyDto>>.Ok([new CurrencyDto("EUR", "Euro", 2)]));

    public Task<ApiResult<IReadOnlyList<IngredientCategoryDto>>> GetIngredientCategoriesAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<IngredientCategoryDto>>.Ok([new IngredientCategoryDto("flour", "Flour", true, false), new IngredientCategoryDto("water", "Water", false, true)]));

    public Task<ApiResult<IReadOnlyList<FlourTypeDto>>> GetFlourTypesAsync(CancellationToken ct = default) =>
        Task.FromResult(ApiResult<IReadOnlyList<FlourTypeDto>>.Ok([new FlourTypeDto("wheat", "Wheat")]));
}
