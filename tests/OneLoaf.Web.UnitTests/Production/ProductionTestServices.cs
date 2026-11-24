using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Production.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;
using OneLoaf.Web.Shared.Device;

namespace OneLoaf.Web.UnitTests.Production;

/// <summary>Registers what every Production effect needs, with a fake API.</summary>
internal static class ProductionTestServices
{
    public static void AddProductionFakes(this BunitContext context, IProductionApi api, TimeProvider? time = null)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(time ?? TimeProvider.System);
        context.Services.AddScoped<DeviceContext>();
        context.Services.AddSingleton(api);
        context.Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly, typeof(ProductionFeature).Assembly));
        context.Render<Fluxor.Blazor.Web.StoreInitializer>();
    }

    public static StageActionRequest NoNote { get; } = new(null, null);
}

/// <summary>Fake production API: sensible empty answers, overridable per test.</summary>
internal class ProductionApiStub : IProductionApi
{
    public static readonly RecipeSummaryDto Country = new(Guid.NewGuid(), "Country sourdough", null, 1, false, DateTimeOffset.UnixEpoch);
    public static readonly StarterDto Rye = new(Guid.NewGuid(), "Mother rye", null, 100m, "regular", 24, 6m, true, null,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(24), DateTimeOffset.UnixEpoch.AddHours(6), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    public virtual Task<ApiResult<PagedResult<BatchSummaryDto>>> ListBatchesAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct = default) =>
        Ok(new PagedResult<BatchSummaryDto>([], 1, 100, 0));

    public virtual Task<ApiResult<BatchDto>> GetBatchAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Like the API: a preview is not saved, so its items have no ids yet.</summary>
    public virtual Task<ApiResult<BatchDto>> PreviewAsync(BatchRequest request, CancellationToken ct = default)
    {
        var batch = Batch(request);
        return Ok(batch with { Items = [.. batch.Items.Select(i => i with { Id = Guid.Empty })] });
    }

    public virtual Task<ApiResult<BatchDto>> CreateBatchAsync(BatchRequest request, CancellationToken ct = default) => Ok(Batch(request));

    public virtual Task<ApiResult> CancelBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

    public virtual Task<ApiResult> DeleteBatchAsync(Guid id, bool ingredientsUsed, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

    public virtual Task<ApiResult<CompleteBatchResponse>> CompleteBatchAsync(Guid id, CancellationToken ct = default) => Ok(new CompleteBatchResponse(Guid.NewGuid()));

    public virtual Task<ApiResult<SaveAsVariantResponse>> SaveAsVariantAsync(Guid id, SaveAsVariantRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<KneadingPlanDto>> GetKneadingPlanAsync(Guid id, CancellationToken ct = default) => Ok(new KneadingPlanDto(id, [], null, null));

    public virtual Task<ApiResult<BatchDto>> StageActionAsync(Guid batchId, Guid stageId, string action, StageActionRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<BatchDto>> ExtendStageAsync(Guid batchId, Guid stageId, int minutes, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<BatchDto>> ChangeDurationsAsync(Guid batchId, Guid stageId, StageDurationRequest request, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<BatchDto>> ResetStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListRecipesAsync(CancellationToken ct = default) =>
        Ok(new PagedResult<RecipeSummaryDto>([Country], 1, 100, 1));

    public virtual Task<ApiResult<StartWindowDto>> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken ct = default) =>
        Ok(new StartWindowDto(recipeId, readyAt, readyAt.AddHours(-18), readyAt.AddHours(-15)));

    public virtual Task<ApiResult<PagedResult<StarterDto>>> ListStartersAsync(CancellationToken ct = default) => Ok(new PagedResult<StarterDto>([Rye], 1, 100, 1));

    public virtual Task<ApiResult<StarterDto>> GetStarterAsync(Guid id, CancellationToken ct = default) => Ok(Rye);

    public virtual Task<ApiResult<StarterDto>> CreateStarterAsync(StarterRequest request, CancellationToken ct = default) => Ok(Rye);

    public virtual Task<ApiResult<StarterDto>> UpdateStarterAsync(Guid id, StarterRequest request, CancellationToken ct = default) => Ok(Rye);

    public virtual Task<ApiResult> DeleteStarterAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

    public virtual Task<ApiResult<PagedResult<FeedingDto>>> ListFeedingsAsync(Guid starterId, CancellationToken ct = default) => Ok(new PagedResult<FeedingDto>([], 1, 20, 0));

    public virtual Task<ApiResult<FeedingDto>> AddFeedingAsync(Guid starterId, FeedingRequest request, CancellationToken ct = default) =>
        Ok(new FeedingDto(Guid.NewGuid(), request.FedAt, request.FlourG, request.WaterG, request.StarterKeptG, request.TemperatureC, null, null, request.Note));

    public virtual Task<ApiResult<PagedResult<FlourDto>>> ListFloursAsync(CancellationToken ct = default) =>
        Ok(new PagedResult<FlourDto>([], 1, 100, 0));

    public virtual Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default) =>
        Ok(new PagedResult<IngredientDto>([], 1, 100, 0));

    public virtual Task<ApiResult<IReadOnlyList<ReminderDto>>> ListRemindersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        Ok<IReadOnlyList<ReminderDto>>([]);

    public static BatchDto Batch(BatchRequest request) => new(Guid.NewGuid(), request.Name, BatchStatuses.Planned, "manual", request.PlannedStartAt,
        request.PlannedStartAt.AddHours(15), request.PlannedStartAt.AddHours(18), null, null, new DateOnly(2026, 11, 23), 8.94m, "EUR", request.Notes,
        [.. request.Items.Select((item, i) => new BatchItemDto(Guid.NewGuid(), item.RecipeId, "Country sourdough", item.Loaves, item.StarterId,
            item.StarterId is null ? null : 300m, 1, item.RemindToFeedStarter ? 360 : null, i + 1, []))],
        null);

    protected static Task<ApiResult<T>> Ok<T>(T value) => Task.FromResult(ApiResult<T>.Ok(value));
}
