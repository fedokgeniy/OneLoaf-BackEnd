using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.Services;

/// <summary>Typed client of the ingredient, price, flour and kitchen endpoints (US-10–13, US-47–49).</summary>
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

    /// <summary><c>GET flours</c>.</summary>
    Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default);

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
    public Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<FlourDto>>(Url.Build("api/v1/flours", ("pageSize", ICatalogApi.MaxPageSize)), ct);

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
