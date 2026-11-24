using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

/// <summary>Base for fake recipe APIs: every call fails the test unless the fake overrides it.</summary>
internal class RecipesApiStub : IRecipesApi
{
    public virtual Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default) => Fail<PagedResult<RecipeSummaryDto>>();

    public virtual Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) => Fail<RecipeDto>();

    public virtual Task<ApiResult<ScaledRecipeDto>> ScaleAsync(Guid id, int loaves, CancellationToken ct = default) => Fail<ScaledRecipeDto>();

    public virtual Task<ApiResult<CostBreakdownDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct = default) => Fail<CostBreakdownDto>();

    public virtual Task<ApiResult<ProductionFiguresDto>> GetProductionFiguresAsync(Guid id, int loaves, CancellationToken ct = default) => Fail<ProductionFiguresDto>();

    public virtual Task<ApiResult<TimelineDto>> GetTimelineAsync(Guid id, DateTimeOffset start, CancellationToken ct = default) => Fail<TimelineDto>();

    public virtual Task<ApiResult<RecipeDto>> CreateAsync(RecipeRequest request, CancellationToken ct = default) => Fail<RecipeDto>();

    public virtual Task<ApiResult<RecipeDto>> UpdateAsync(Guid id, RecipeRequest request, CancellationToken ct = default) => Fail<RecipeDto>();

    public virtual Task<ApiResult> DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult> SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<RecipeDto>> CreateVariantAsync(Guid id, CreateVariantRequest request, CancellationToken ct = default) => Fail<RecipeDto>();

    public virtual Task<ApiResult<RecipeComparisonDto>> CompareAsync(Guid id, CancellationToken ct = default) => Fail<RecipeComparisonDto>();

    private static Task<ApiResult<T>> Fail<T>() => throw new NotSupportedException($"Unexpected call returning {typeof(T).Name}.");

    public virtual Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ApiResult<FileDownload>.Fail(ApiError.FromStatus(404)));

    public virtual Task<ApiResult> SetPhotoAsync(Guid id, OneLoaf.Web.Shared.Ui.PhotoFile photo, CancellationToken ct = default) =>
        Task.FromResult(ApiResult.Ok);

    public virtual Task<ApiResult> DeletePhotoAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);
}
