using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;

namespace OneLoaf.Web.Features.Production.Services;

/// <summary>Typed client of the batch, starter and reminder endpoints (US-20–30).</summary>
public interface IProductionApi
{
    /// <summary><c>GET batches?from=&amp;to=&amp;status=</c>.</summary>
    Task<ApiResult<PagedResult<BatchSummaryDto>>> ListBatchesAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct = default);

    /// <summary><c>GET batches/{id}</c>.</summary>
    Task<ApiResult<BatchDto>> GetBatchAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST batches/preview</c>: the batch as it would be saved, nothing stored (US-20).</summary>
    Task<ApiResult<BatchDto>> PreviewAsync(BatchRequest request, CancellationToken ct = default);

    /// <summary><c>POST batches</c>.</summary>
    Task<ApiResult<BatchDto>> CreateBatchAsync(BatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>POST batches/{id}/cancel?ingredientsUsed=</c>: false returns the ingredients the batch took to the stock.
    /// </summary>
    Task<ApiResult> CancelBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default);

    /// <summary><c>DELETE batches/{id}?ingredientsUsed=</c>: 409 while the batch is running.</summary>
    Task<ApiResult> DeleteBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default);

    /// <summary><c>POST batches/{id}/complete</c> (US-31).</summary>
    Task<ApiResult<CompleteBatchResponse>> CompleteBatchAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST batches/{id}/save-as-variant</c> (US-25).</summary>
    Task<ApiResult<SaveAsVariantResponse>> SaveAsVariantAsync(Guid id, SaveAsVariantRequest request, CancellationToken ct = default);

    /// <summary><c>GET batches/{id}/kneading-plan</c> (US-22).</summary>
    Task<ApiResult<KneadingPlanDto>> GetKneadingPlanAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST batches/{id}/stages/{stageId}/{action}</c> for start, complete, skip (US-23).</summary>
    Task<ApiResult<BatchDto>> StageActionAsync(Guid batchId, Guid stageId, string action, StageActionRequest request, CancellationToken ct = default);

    /// <summary><c>POST …/extend</c> (US-24).</summary>
    Task<ApiResult<BatchDto>> ExtendStageAsync(Guid batchId, Guid stageId, int minutes, CancellationToken ct = default);

    /// <summary><c>PATCH …/stages/{stageId}</c> (US-21).</summary>
    Task<ApiResult<BatchDto>> ChangeDurationsAsync(Guid batchId, Guid stageId, StageDurationRequest request, CancellationToken ct = default);

    /// <summary><c>POST …/reset</c>: back to the recipe's durations.</summary>
    Task<ApiResult<BatchDto>> ResetStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default);

    /// <summary><c>GET recipes</c> (first 100, not archived) for the batch form.</summary>
    Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListRecipesAsync(CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/start-window?readyAt=</c> (US-52, plan backwards).</summary>
    Task<ApiResult<StartWindowDto>> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken ct = default);

    /// <summary><c>GET starters</c>.</summary>
    Task<ApiResult<PagedResult<StarterDto>>> ListStartersAsync(CancellationToken ct = default);

    /// <summary><c>GET starters/{id}</c>.</summary>
    Task<ApiResult<StarterDto>> GetStarterAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST starters</c> (US-27).</summary>
    Task<ApiResult<StarterDto>> CreateStarterAsync(StarterRequest request, CancellationToken ct = default);

    /// <summary><c>PUT starters/{id}</c>.</summary>
    Task<ApiResult<StarterDto>> UpdateStarterAsync(Guid id, StarterRequest request, CancellationToken ct = default);

    /// <summary><c>DELETE starters/{id}</c>.</summary>
    Task<ApiResult> DeleteStarterAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET starters/{id}/feedings</c>, newest first.</summary>
    Task<ApiResult<PagedResult<FeedingDto>>> ListFeedingsAsync(Guid starterId, CancellationToken ct = default);

    /// <summary><c>POST starters/{id}/feedings</c> (US-28).</summary>
    Task<ApiResult<FeedingDto>> AddFeedingAsync(Guid starterId, FeedingRequest request, CancellationToken ct = default);

    /// <summary><c>GET flours</c> for the starter form.</summary>
    Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default);

    /// <summary><c>POST flours</c>: adds a flour from the starter form (US-11); 409 duplicate name.</summary>
    Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default);

    /// <summary><c>GET ingredients</c>: the ingredients a starter can be fed with.</summary>
    Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default);

    /// <summary>Notification <c>GET reminders?status=scheduled&amp;from=&amp;to=</c> (US-29).</summary>
    Task<ApiResult<IReadOnlyList<ReminderDto>>> ListRemindersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ProductionApi(ApiClientFactory api) : IProductionApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<BatchSummaryDto>>> ListBatchesAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<BatchSummaryDto>>(Url.Build("api/v1/batches", ("from", from), ("to", to), ("status", status), ("pageSize", 100)), ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> GetBatchAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<BatchDto>($"api/v1/batches/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> PreviewAsync(BatchRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<BatchRequest, BatchDto>("api/v1/batches/preview", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> CreateBatchAsync(BatchRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<BatchRequest, BatchDto>("api/v1/batches", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> CancelBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default) =>
        api.Core.PostAsync(Url.Build($"api/v1/batches/{id}/cancel", ("ingredientsUsed", ingredientsUsed ? "true" : "false")), ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default) =>
        api.Core.DeleteAsync(Url.Build($"api/v1/batches/{id}", ("ingredientsUsed", ingredientsUsed ? "true" : "false")), ct);

    /// <inheritdoc />
    public Task<ApiResult<CompleteBatchResponse>> CompleteBatchAsync(Guid id, CancellationToken ct = default) =>
        api.Core.PostEmptyAsync<CompleteBatchResponse>($"api/v1/batches/{id}/complete", ct);

    /// <inheritdoc />
    public Task<ApiResult<SaveAsVariantResponse>> SaveAsVariantAsync(Guid id, SaveAsVariantRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<SaveAsVariantRequest, SaveAsVariantResponse>($"api/v1/batches/{id}/save-as-variant", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<KneadingPlanDto>> GetKneadingPlanAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<KneadingPlanDto>($"api/v1/batches/{id}/kneading-plan", ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> StageActionAsync(Guid batchId, Guid stageId, string action, StageActionRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<StageActionRequest, BatchDto>($"api/v1/batches/{batchId}/stages/{stageId}/{action}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> ExtendStageAsync(Guid batchId, Guid stageId, int minutes, CancellationToken ct = default) =>
        api.Core.PostAsync<ExtendStageRequest, BatchDto>($"api/v1/batches/{batchId}/stages/{stageId}/extend", new ExtendStageRequest(minutes), ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> ChangeDurationsAsync(Guid batchId, Guid stageId, StageDurationRequest request, CancellationToken ct = default) =>
        api.Core.PatchAsync<StageDurationRequest, BatchDto>($"api/v1/batches/{batchId}/stages/{stageId}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<BatchDto>> ResetStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default) =>
        api.Core.PostEmptyAsync<BatchDto>($"api/v1/batches/{batchId}/stages/{stageId}/reset", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListRecipesAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<RecipeSummaryDto>>(Url.Build("api/v1/recipes", ("pageSize", 100)), ct);

    /// <inheritdoc />
    public Task<ApiResult<StartWindowDto>> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken ct = default) =>
        api.Core.GetAsync<StartWindowDto>(Url.Build($"api/v1/recipes/{recipeId}/start-window", ("readyAt", readyAt)), ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<StarterDto>>> ListStartersAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<StarterDto>>(Url.Build("api/v1/starters", ("pageSize", 100)), ct);

    /// <inheritdoc />
    public Task<ApiResult<StarterDto>> GetStarterAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<StarterDto>($"api/v1/starters/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<StarterDto>> CreateStarterAsync(StarterRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<StarterRequest, StarterDto>("api/v1/starters", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<StarterDto>> UpdateStarterAsync(Guid id, StarterRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<StarterRequest, StarterDto>($"api/v1/starters/{id}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteStarterAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/starters/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<FeedingDto>>> ListFeedingsAsync(Guid starterId, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<FeedingDto>>(Url.Build($"api/v1/starters/{starterId}/feedings", ("pageSize", 20)), ct);

    /// <inheritdoc />
    public Task<ApiResult<FeedingDto>> AddFeedingAsync(Guid starterId, FeedingRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<FeedingRequest, FeedingDto>($"api/v1/starters/{starterId}/feedings", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<FlourDto>>(Url.Build("api/v1/flours", ("pageSize", 100)), ct);

    /// <inheritdoc />
    public Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<FlourRequest, FlourDto>("api/v1/flours", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<IngredientDto>>(Url.Build("api/v1/ingredients", ("pageSize", 100)), ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<ReminderDto>>> ListRemindersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        api.Notification.GetAsync<IReadOnlyList<ReminderDto>>(Url.Build("api/v1/reminders", ("status", "scheduled"), ("from", from), ("to", to)), ct);
}
