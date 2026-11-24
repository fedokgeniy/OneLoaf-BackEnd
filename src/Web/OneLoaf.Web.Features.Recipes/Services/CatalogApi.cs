using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.Services;

/// <summary>Typed client of the ingredient, price, stock, flour and kitchen endpoints (US-10–13, US-47–49).</summary>
public interface ICatalogApi
{
    /// <summary>Page size of ingredient lists; the editor loads one page of 100, enough for a home or pilot bakery.</summary>
    const int MaxPageSize = 100;

    /// <summary><c>GET ingredients?search=&amp;category=&amp;page=</c>.</summary>
    Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default);

    /// <summary><c>POST ingredients</c>: 201, 400, 409 duplicate name.</summary>
    Task<ApiResult<IngredientDto>> CreateIngredientAsync(IngredientRequest request, CancellationToken ct = default);

    /// <summary><c>PUT ingredients/{id}</c>.</summary>
    Task<ApiResult<IngredientDto>> UpdateIngredientAsync(Guid id, IngredientRequest request, CancellationToken ct = default);

    /// <summary><c>DELETE ingredients/{id}</c>: 409 when used in a recipe.</summary>
    Task<ApiResult> DeleteIngredientAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET ingredients/{id}/prices</c>: price history, newest first.</summary>
    Task<ApiResult<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST ingredients/{id}/prices</c>: 409 when a price for that date exists (US-12).</summary>
    Task<ApiResult<PriceDto>> AddPriceAsync(Guid id, PriceRequest request, CancellationToken ct = default);

    /// <summary><c>PUT ingredients/{id}/prices/{priceId}</c>: 409 when another price has the new date.</summary>
    Task<ApiResult<PriceDto>> UpdatePriceAsync(Guid id, Guid priceId, PriceRequest request, CancellationToken ct = default);

    /// <summary><c>DELETE ingredients/{id}/prices/{priceId}</c>.</summary>
    Task<ApiResult> DeletePriceAsync(Guid id, Guid priceId, CancellationToken ct = default);

    /// <summary><c>GET ingredients/{id}/stock-movements</c>: the stock ledger, newest first.</summary>
    Task<ApiResult<PagedResult<StockMovementDto>>> ListMovementsAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST ingredients/{id}/purchases</c>: adds to the stock and sets the price of the purchase date.</summary>
    Task<ApiResult<StockMovementDto>> AddPurchaseAsync(Guid id, PurchaseRequest request, CancellationToken ct = default);

    /// <summary><c>POST ingredients/{id}/stock-adjustments</c>: sets the stock to the counted quantity.</summary>
    Task<ApiResult<StockMovementDto>> AdjustStockAsync(Guid id, StockAdjustmentRequest request, CancellationToken ct = default);

    /// <summary><c>GET flours</c>.</summary>
    Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default);

    /// <summary><c>POST flours</c>: 409 duplicate name (US-11).</summary>
    Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default);

    /// <summary><c>PUT flours/{id}</c>.</summary>
    Task<ApiResult<FlourDto>> UpdateFlourAsync(Guid id, FlourRequest request, CancellationToken ct = default);

    /// <summary><c>DELETE flours/{id}</c>: ingredients and starters keep existing without the flour.</summary>
    Task<ApiResult> DeleteFlourAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET warnings</c>: data quality issues such as ingredients without a price.</summary>
    Task<ApiResult<IReadOnlyList<WarningDto>>> GetWarningsAsync(CancellationToken ct = default);

    /// <summary><c>GET kitchen-settings</c>: 404 before the first save.</summary>
    Task<ApiResult<KitchenSettingsDto>> GetKitchenAsync(CancellationToken ct = default);

    /// <summary><c>PUT kitchen-settings</c>.</summary>
    Task<ApiResult<KitchenSettingsDto>> SaveKitchenAsync(KitchenSettingsRequest request, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class CatalogApi(ApiClientFactory api) : ICatalogApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(
        string? search, string? category, int page, int pageSize, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<IngredientDto>>(
            Url.Build("api/v1/ingredients", ("search", search), ("category", category), ("page", page), ("pageSize", pageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult<IngredientDto>> CreateIngredientAsync(IngredientRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<IngredientRequest, IngredientDto>("api/v1/ingredients", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<IngredientDto>> UpdateIngredientAsync(Guid id, IngredientRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<IngredientRequest, IngredientDto>($"api/v1/ingredients/{id}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteIngredientAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/ingredients/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<IReadOnlyList<PriceDto>>($"api/v1/ingredients/{id}/prices", ct);

    /// <inheritdoc />
    public Task<ApiResult<PriceDto>> AddPriceAsync(Guid id, PriceRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<PriceRequest, PriceDto>($"api/v1/ingredients/{id}/prices", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<PriceDto>> UpdatePriceAsync(Guid id, Guid priceId, PriceRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<PriceRequest, PriceDto>($"api/v1/ingredients/{id}/prices/{priceId}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeletePriceAsync(Guid id, Guid priceId, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/ingredients/{id}/prices/{priceId}", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<StockMovementDto>>> ListMovementsAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<StockMovementDto>>(
            Url.Build($"api/v1/ingredients/{id}/stock-movements", ("pageSize", ICatalogApi.MaxPageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult<StockMovementDto>> AddPurchaseAsync(Guid id, PurchaseRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<PurchaseRequest, StockMovementDto>($"api/v1/ingredients/{id}/purchases", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<StockMovementDto>> AdjustStockAsync(Guid id, StockAdjustmentRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<StockAdjustmentRequest, StockMovementDto>($"api/v1/ingredients/{id}/stock-adjustments", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<FlourDto>>(Url.Build("api/v1/flours", ("pageSize", ICatalogApi.MaxPageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<FlourRequest, FlourDto>("api/v1/flours", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<FlourDto>> UpdateFlourAsync(Guid id, FlourRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<FlourRequest, FlourDto>($"api/v1/flours/{id}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteFlourAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/flours/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<WarningDto>>> GetWarningsAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<IReadOnlyList<WarningDto>>("api/v1/warnings", ct);

    /// <inheritdoc />
    public Task<ApiResult<KitchenSettingsDto>> GetKitchenAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<KitchenSettingsDto>("api/v1/kitchen-settings", ct);

    /// <inheritdoc />
    public Task<ApiResult<KitchenSettingsDto>> SaveKitchenAsync(KitchenSettingsRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<KitchenSettingsRequest, KitchenSettingsDto>("api/v1/kitchen-settings", request, ct);
}
