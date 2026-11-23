using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;
using OneLoaf.SharedKernel.Text;

namespace OneLoaf.CoreApi.Application.Batches;

/// <summary>Limits of batch requests.</summary>
public static class BatchLimits
{
    /// <summary><c>batches.name</c>.</summary>
    public const int NameMaxLength = 200;

    /// <summary><c>batches.notes</c>.</summary>
    public const int NotesMaxLength = 4000;

    /// <summary><c>batch_stages.note</c>.</summary>
    public const int StageNoteMaxLength = 2000;

    /// <summary>Largest number of items in a batch.</summary>
    public const int MaxItems = 20;

    /// <summary>Longest starter reminder and stage extension, minutes (one day).</summary>
    public const int MaxMinutes = 24 * 60;
}

/// <summary>Validates <see cref="BatchRequest"/>.</summary>
public sealed class BatchRequestValidator : AbstractValidator<BatchRequest>
{
    /// <summary>Creates the rules.</summary>
    public BatchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(BatchLimits.NameMaxLength);
        RuleFor(x => x.PlannedStartAt).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.Notes).MaximumLength(BatchLimits.NotesMaxLength);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Items).NotNull().NotEmpty().Must(i => i.Count <= BatchLimits.MaxItems);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.RecipeId).NotEmpty();
            item.RuleFor(i => i.Loaves).InclusiveBetween(1, RecipeLimits.MaxLoaves);
            item.RuleFor(i => i.StarterId).NotEqual(Guid.Empty);
            item.RuleFor(i => i.StarterReminderMinutes).InclusiveBetween(0, BatchLimits.MaxMinutes);
        });
    }
}

/// <summary>Validates <see cref="BatchListQuery"/>.</summary>
public sealed class BatchListQueryValidator : AbstractValidator<BatchListQuery>
{
    /// <summary>Creates the rules.</summary>
    public BatchListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Status).Must(WireEnumNames.IsNullOrDefined<BatchStatus>)
            .WithMessage($"status must be one of: {WireEnumNames.Values<BatchStatus>()}.");
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
    }
}

/// <summary>Validates <see cref="StageActionRequest"/>.</summary>
public sealed class StageActionRequestValidator : AbstractValidator<StageActionRequest>
{
    /// <summary>Creates the rules.</summary>
    public StageActionRequestValidator()
    {
        RuleFor(x => x.ActualTemperatureC).InclusiveBetween(RecipeLimits.MinTemperatureC, RecipeLimits.MaxTemperatureC);
        RuleFor(x => x.Note).MaximumLength(BatchLimits.StageNoteMaxLength);
    }
}

/// <summary>Validates <see cref="ExtendStageRequest"/>.</summary>
public sealed class ExtendStageRequestValidator : AbstractValidator<ExtendStageRequest>
{
    /// <summary>Creates the rules.</summary>
    public ExtendStageRequestValidator() => RuleFor(x => x.Minutes).InclusiveBetween(1, BatchLimits.MaxMinutes);
}

/// <summary>Validates <see cref="StageDurationRequest"/>.</summary>
public sealed class StageDurationRequestValidator : AbstractValidator<StageDurationRequest>
{
    /// <summary>Creates the rules.</summary>
    public StageDurationRequestValidator()
    {
        RuleFor(x => x.PlannedMinMinutes).InclusiveBetween(0, RecipeLimits.MaxStageMinutes);
        RuleFor(x => x.PlannedMaxMinutes).InclusiveBetween(0, RecipeLimits.MaxStageMinutes).GreaterThanOrEqualTo(x => x.PlannedMinMinutes);
    }
}

/// <summary>Production batches and stages of the current user (US-20 - US-24, US-26, US-31).</summary>
public sealed partial class BatchService(
    IBatchRepository batches,
    IRecipeRepository recipes,
    IStarterRepository starters,
    IRecipeCalculations calculations,
    KitchenSettingsService kitchen,
    IEventPublisher events,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<BatchService> logger)
{
    /// <summary>Message for a missing batch.</summary>
    public const string NotFoundMessage = "The batch was not found.";

    /// <summary>Message for a missing stage.</summary>
    public const string StageNotFoundMessage = "The stage was not found.";

    /// <summary>Message when a started batch is re-planned.</summary>
    public const string NotPlannedMessage = "Only a planned batch can be changed.";

    /// <summary>Message when a finished batch is cancelled.</summary>
    public const string NotActiveMessage = "The batch is already finished.";

    /// <summary>Message when a batch that has not started is completed.</summary>
    public const string NotStartedMessage = "Start the first stage before completing the batch.";

    /// <summary>Message for a stage action not allowed in the stage's state.</summary>
    public const string StageStateMessage = "The action is not allowed for the stage in its current state.";

    /// <summary>Message when a stage without its recipe stage is reset.</summary>
    public const string NoRecipeStageMessage = "The recipe stage of this batch stage no longer exists.";

    /// <summary>Page of the calendar.</summary>
    public Task<PagedResult<BatchSummaryDto>> ListAsync(BatchListQuery query, CancellationToken cancellationToken) =>
        batches.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>One batch with items and stages.</summary>
    public async Task<BatchDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await batches.GetAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>The full plan of a batch without saving it (US-34 support).</summary>
    public async Task<BatchDto> PreviewAsync(BatchRequest request, CancellationToken cancellationToken) =>
        await batches.PlanAsync(currentUser.Id, await PrepareAsync(request, cancellationToken), save: false, cancellationToken);

    /// <summary>Saves a batch and builds its plan with <c>sp_build_batch_plan</c> (US-20); publishes <c>batch.planned</c>.</summary>
    public async Task<BatchDto> CreateAsync(BatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await batches.PlanAsync(currentUser.Id, await PrepareAsync(request, cancellationToken), save: true, cancellationToken);
        LogPlanned(logger, batch.Id, currentUser.Id);
        await events.PublishAsync(new BatchPlanned(batch.Id, currentUser.Id, batch.Name, batch.PlannedStartAt,
            batch.PlannedReadyFrom, batch.PlannedReadyTo, FirstStageStart(batch), timeProvider.GetUtcNow()), cancellationToken);
        return batch;
    }

    /// <summary>Replaces the items of a planned batch and plans it again.</summary>
    public async Task<BatchDto> ReplanAsync(Guid id, BatchRequest request, CancellationToken cancellationToken)
    {
        var status = await batches.GetStatusAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        if (status != BatchStatus.Planned)
        {
            throw new ConflictException(NotPlannedMessage);
        }

        var prepared = await PrepareAsync(request, cancellationToken);
        return await batches.ReplanAsync(currentUser.Id, id, prepared, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <summary>Cancels a planned or running batch; the database cancels its reminders.</summary>
    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var status = await batches.GetStatusAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        if (status is not (BatchStatus.Planned or BatchStatus.InProgress))
        {
            throw new ConflictException(NotActiveMessage);
        }

        await batches.SetStatusAsync(currentUser.Id, id, BatchStatus.Cancelled, cancellationToken);
    }

    /// <summary>
    /// Completes a running batch; the database creates the history record with the cost on the bake date (US-31).
    /// Refreshes the statistics and publishes <c>batch.completed</c>.
    /// </summary>
    public async Task<CompleteBatchResponse> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var status = await batches.GetStatusAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        if (status != BatchStatus.InProgress)
        {
            throw new ConflictException(status == BatchStatus.Planned ? NotStartedMessage : NotActiveMessage);
        }

        await batches.SetStatusAsync(currentUser.Id, id, BatchStatus.Done, cancellationToken);
        var bakeRecordId = await batches.GetBakeRecordIdAsync(id, cancellationToken);
        await batches.RefreshStatisticsAsync(cancellationToken);
        LogCompleted(logger, id, currentUser.Id);
        await events.PublishAsync(new BatchCompleted(id, currentUser.Id, bakeRecordId, timeProvider.GetUtcNow()), cancellationToken);
        return new CompleteBatchResponse(bakeRecordId);
    }

    /// <summary>Starts a pending stage now (US-23).</summary>
    public async Task<BatchDto> StartStageAsync(Guid batchId, Guid stageId, StageActionRequest action, CancellationToken cancellationToken)
    {
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status == StageStatus.Pending);
        await batches.StartStageAsync(stageId, timeProvider.GetUtcNow(), action, cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Completes a running stage now (US-23).</summary>
    public async Task<BatchDto> CompleteStageAsync(Guid batchId, Guid stageId, StageActionRequest action, CancellationToken cancellationToken)
    {
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status == StageStatus.Running);
        await batches.CompleteStageAsync(stageId, timeProvider.GetUtcNow(), action, cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Skips a pending stage (optional stages, US-09).</summary>
    public async Task<BatchDto> SkipStageAsync(Guid batchId, Guid stageId, StageActionRequest action, CancellationToken cancellationToken)
    {
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status == StageStatus.Pending);
        await batches.SkipStageAsync(stageId, action, cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Extends a pending or running stage with <c>sp_extend_batch_stage</c> (US-24).</summary>
    public async Task<BatchDto> ExtendStageAsync(Guid batchId, Guid stageId, int minutes, CancellationToken cancellationToken)
    {
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status is StageStatus.Pending or StageStatus.Running);
        await batches.ExtendStageAsync(stageId, minutes, cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Changes the planned durations of a pending stage for this batch only (US-21).</summary>
    public async Task<BatchDto> ChangeStageDurationsAsync(
        Guid batchId, Guid stageId, StageDurationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status == StageStatus.Pending);
        await batches.ChangeStageDurationsAsync(stageId, request.PlannedMinMinutes, request.PlannedMaxMinutes, overridden: true,
            cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Restores the durations of the recipe stage (US-21).</summary>
    public async Task<BatchDto> ResetStageAsync(Guid batchId, Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await GetActiveStageAsync(batchId, stageId, cancellationToken);
        EnsureStage(stage.Status == StageStatus.Pending);
        var durations = stage.RecipeStageId is { } recipeStageId
            ? await batches.GetRecipeStageDurationsAsync(recipeStageId, cancellationToken)
            : null;
        if (durations is not { } recipe)
        {
            throw new ConflictException(NoRecipeStageMessage);
        }

        await batches.ChangeStageDurationsAsync(stageId, recipe.Min, recipe.Max, overridden: false, cancellationToken);
        return await GetAsync(batchId, cancellationToken);
    }

    /// <summary>Kneads of the same dough merged across items (US-22).</summary>
    public async Task<KneadingPlanDto> GetKneadingPlanAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var items = await batches.GetKneadingItemsAsync(currentUser.Id, batchId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        var figures = new Dictionary<Guid, ProductionFigures>();
        foreach (var group in items.GroupBy(i => i.RecipeId))
        {
            figures[group.Key] = await calculations.GetProductionFiguresAsync(group.Key, group.Sum(i => i.Loaves), cancellationToken);
        }

        return KneadingPlanner.Plan(batchId, items, figures);
    }

    private async Task<NewBatch> PrepareAsync(BatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (!await recipes.ExistsAsync(currentUser.Id, item.RecipeId, cancellationToken))
            {
                throw ValidationFailures.For($"items[{i}].recipeId", RecipeService.NotFoundMessage);
            }

            if (item.StarterId is { } starterId && !await starters.ExistsAsync(currentUser.Id, starterId, cancellationToken))
            {
                throw ValidationFailures.For($"items[{i}].starterId", StarterService.NotFoundMessage);
            }
        }

        var settings = await kitchen.GetAsync(cancellationToken);
        var priceDate = request.PriceDate ?? await TodayInKitchenAsync(cancellationToken);
        return new NewBatch(request.Name.Trim(), request.Source, request.PlannedStartAt, priceDate, settings.Currency,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), request.Items);
    }

    private async Task<DateOnly> TodayInKitchenAsync(CancellationToken cancellationToken)
    {
        var zone = await kitchen.GetTimeZoneAsync(cancellationToken);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }

    private async Task<StageState> GetActiveStageAsync(Guid batchId, Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await batches.GetStageAsync(currentUser.Id, batchId, stageId, cancellationToken)
            ?? throw new NotFoundException(StageNotFoundMessage);
        if (stage.BatchStatus is not (BatchStatus.Planned or BatchStatus.InProgress))
        {
            throw new ConflictException(NotActiveMessage);
        }

        return stage;
    }

    private static void EnsureStage(bool allowed)
    {
        if (!allowed)
        {
            throw new ConflictException(StageStateMessage);
        }
    }

    private static DateTimeOffset? FirstStageStart(BatchDto batch) =>
        batch.Items.SelectMany(i => i.Stages).Select(s => (DateTimeOffset?)s.PlannedStartAt).Min();

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch {BatchId} planned for {UserId}")]
    private static partial void LogPlanned(ILogger logger, Guid batchId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch {BatchId} completed by {UserId}")]
    private static partial void LogCompleted(ILogger logger, Guid batchId, Guid userId);
}

/// <summary>
/// Merges kneads of the same dough (US-22): items made from the same recipe can be kneaded together; the number of
/// cycles for the merged dough comes from <c>fn_kneading_cycles</c> for the total loaves.
/// </summary>
public static class KneadingPlanner
{
    /// <summary>Builds one group per recipe with the cycles separately and merged and an explanation.</summary>
    public static KneadingPlanDto Plan(Guid batchId, IReadOnlyList<KneadingItem> items, IReadOnlyDictionary<Guid, ProductionFigures> mergedFigures)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(mergedFigures);

        var groups = items.GroupBy(i => i.RecipeId).Select(group =>
        {
            var list = group.ToList();
            var figures = mergedFigures[group.Key];
            var loaves = list.Sum(i => i.Loaves);
            int? separate = list.All(i => i.KneadingCycles.HasValue) ? list.Sum(i => i.KneadingCycles!.Value) : null;
            var merged = figures.KneadingCycles;
            return new KneadingGroupDto(group.Key, list[0].RecipeTitle, list.Select(i => i.ItemId).ToList(), loaves,
                figures.DoughWeightG, separate, merged, Explain(list.Count, list[0].RecipeTitle, loaves, figures.DoughWeightG, separate, merged));
        }).ToList();

        int? totalSeparate = groups.All(g => g.SeparateCycles.HasValue) ? groups.Sum(g => g.SeparateCycles!.Value) : null;
        int? totalMerged = groups.All(g => g.MergedCycles.HasValue) ? groups.Sum(g => g.MergedCycles!.Value) : null;
        return new KneadingPlanDto(batchId, groups, totalSeparate, totalMerged);
    }

    private static string Explain(int itemCount, string title, int loaves, decimal doughWeightG, int? separate, int? merged)
    {
        var weight = doughWeightG.ToString("0.#", CultureInfo.InvariantCulture);
        if (merged is null || separate is null)
        {
            return $"Set the mixer bowl capacity in the kitchen settings to plan kneading of \"{title}\".";
        }

        if (itemCount == 1)
        {
            return $"\"{title}\": {loaves} loaves, {weight} g of dough, {merged} kneading cycle(s).";
        }

        return merged < separate
            ? $"{itemCount} items of \"{title}\" ({loaves} loaves, {weight} g) are kneaded together in {merged} cycle(s) instead of {separate}."
            : $"{itemCount} items of \"{title}\" ({loaves} loaves, {weight} g) need {merged} cycle(s); merging does not save a cycle.";
    }
}
