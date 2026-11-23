using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.SharedKernel.Text;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IBatchRepository"/>. The plan (stages, kneading, starter, reminders, ready
/// window, cost) is built by <c>sp_build_batch_plan</c> in the same transaction as the insert.
/// </summary>
public sealed class BatchRepository(OneLoafDbContext db) : IBatchRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<BatchSummaryDto>> ListAsync(Guid ownerId, BatchListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var batches = db.Batches.AsNoTracking().Where(b => b.OwnerId == ownerId);
        if (query.From is { } from)
        {
            var fromUtc = from.UtcDateTime;
            batches = batches.Where(b => b.PlannedStartAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.UtcDateTime;
            batches = batches.Where(b => b.PlannedStartAt < toUtc);
        }

        if (WireEnumNames.ParseOrNull<BatchStatus>(query.Status) is { } status)
        {
            batches = batches.Where(b => b.Status == status);
        }

        var total = await batches.CountAsync(cancellationToken);
        var rows = await batches.OrderBy(b => b.PlannedStartAt).ThenBy(b => b.Id)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .Select(b => new
            {
                b.Id, b.Name, b.Status, b.PlannedStartAt, b.PlannedReadyFrom, b.PlannedReadyTo,
                Loaves = b.Items.Sum(i => i.Loaves),
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(b => new BatchSummaryDto(b.Id, b.Name, b.Status, Utc(b.PlannedStartAt), Utc(b.PlannedReadyFrom),
            Utc(b.PlannedReadyTo), b.Loaves)).ToList();
        return new PagedResult<BatchSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<BatchDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var batch = await db.Batches.AsNoTracking()
            .Include(b => b.Items).ThenInclude(i => i.Recipe)
            .Include(b => b.Items).ThenInclude(i => i.Stages)
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.OwnerId == ownerId && b.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        var bakeRecordId = await GetBakeRecordIdAsync(id, cancellationToken);
        return ToDto(batch, bakeRecordId);
    }

    /// <inheritdoc />
    public async Task<BatchDto> PlanAsync(Guid ownerId, NewBatch batch, bool save, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entity = new Batch
        {
            OwnerId = ownerId,
            Status = BatchStatus.Planned,
            Items = batch.Items.Select(ToItem).ToList(),
        };
        ApplyHeader(entity, batch);
        db.Batches.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"CALL sp_build_batch_plan({entity.Id})", cancellationToken);
        var planned = (await GetAsync(ownerId, entity.Id, cancellationToken))!;

        if (save)
        {
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return planned;
        }

        await transaction.RollbackAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return AsPreview(planned);
    }

    /// <inheritdoc />
    public async Task<BatchDto?> ReplanAsync(Guid ownerId, Guid id, NewBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entity = await db.Batches.Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.OwnerId == ownerId && b.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        ApplyHeader(entity, batch);
        db.BatchItems.RemoveRange(entity.Items);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var item in batch.Items.Select(ToItem))
        {
            item.BatchId = id;
            db.BatchItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"CALL sp_build_batch_plan({id})", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return await GetAsync(ownerId, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BatchStatus?> GetStatusAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Batches.AsNoTracking().Where(b => b.OwnerId == ownerId && b.Id == id)
            .Select(b => (BatchStatus?)b.Status).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task SetStatusAsync(Guid ownerId, Guid id, BatchStatus status, CancellationToken cancellationToken) =>
        db.Batches.Where(b => b.OwnerId == ownerId && b.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, status), cancellationToken);

    /// <inheritdoc />
    public async Task<Guid?> GetBakeRecordIdAsync(Guid batchId, CancellationToken cancellationToken) =>
        await db.BakeRecords.AsNoTracking().Where(r => r.BatchId == batchId).Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task RefreshStatisticsAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT fn_refresh_bake_statistics()", cancellationToken);

    /// <inheritdoc />
    public Task<StageState?> GetStageAsync(Guid ownerId, Guid batchId, Guid stageId, CancellationToken cancellationToken) =>
        db.BatchStages.AsNoTracking()
            .Where(s => s.Id == stageId && s.BatchItem.BatchId == batchId && s.BatchItem.Batch.OwnerId == ownerId)
            .Select(s => new StageState(s.Id, s.Status, s.BatchItem.Batch.Status, s.RecipeStageId, s.PlannedMinMinutes,
                s.PlannedMaxMinutes))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task StartStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var utc = at.UtcDateTime;
        return db.BatchStages.Where(s => s.Id == stageId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ActualStartAt, utc)
            .SetProperty(x => x.ActualTemperatureC, x => action.ActualTemperatureC ?? x.ActualTemperatureC)
            .SetProperty(x => x.Note, x => action.Note ?? x.Note), cancellationToken);
    }

    /// <inheritdoc />
    public Task CompleteStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var utc = at.UtcDateTime;
        return db.BatchStages.Where(s => s.Id == stageId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ActualEndAt, utc)
            .SetProperty(x => x.ActualTemperatureC, x => action.ActualTemperatureC ?? x.ActualTemperatureC)
            .SetProperty(x => x.Note, x => action.Note ?? x.Note), cancellationToken);
    }

    /// <inheritdoc />
    public Task SkipStageAsync(Guid stageId, StageActionRequest action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return db.BatchStages.Where(s => s.Id == stageId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, StageStatus.Skipped)
            .SetProperty(x => x.Note, x => action.Note ?? x.Note), cancellationToken);
    }

    /// <inheritdoc />
    public Task ExtendStageAsync(Guid stageId, int minutes, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"CALL sp_extend_batch_stage({stageId}, {minutes})", cancellationToken);

    /// <inheritdoc />
    public async Task ChangeStageDurationsAsync(Guid stageId, int minMinutes, int maxMinutes, bool overridden, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stage = await db.BatchStages.AsNoTracking().Where(s => s.Id == stageId)
            .Select(s => new { s.BatchItemId, s.BatchItem.BatchId, s.SortOrder, s.PlannedMinMinutes, s.PlannedMaxMinutes })
            .SingleAsync(cancellationToken);
        var deltaMin = minMinutes - stage.PlannedMinMinutes;
        var deltaMax = maxMinutes - stage.PlannedMaxMinutes;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE batch_stages
            SET planned_min_minutes = {minMinutes},
                planned_max_minutes = {maxMinutes},
                planned_end_min_at  = planned_end_min_at + make_interval(mins => {deltaMin}),
                planned_end_max_at  = planned_end_max_at + make_interval(mins => {deltaMax}),
                is_overridden       = {overridden}
            WHERE id = {stageId}
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE batch_stages
            SET planned_start_at   = planned_start_at + make_interval(mins => {deltaMin}),
                planned_end_min_at = planned_end_min_at + make_interval(mins => {deltaMin}),
                planned_end_max_at = planned_end_max_at + make_interval(mins => {deltaMax})
            WHERE batch_item_id = {stage.BatchItemId} AND sort_order > {stage.SortOrder} AND status = 'pending'
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE reminders
            SET due_at = due_at + make_interval(mins => {deltaMin})
            WHERE status = 'scheduled'
              AND batch_stage_id IN (SELECT id FROM batch_stages
                                     WHERE batch_item_id = {stage.BatchItemId} AND sort_order > {stage.SortOrder})
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE batches
            SET planned_ready_from = s.ready_from,
                planned_ready_to   = s.ready_to
            FROM (SELECT max(bs.planned_end_min_at) AS ready_from, max(bs.planned_end_max_at) AS ready_to
                  FROM batch_stages bs JOIN batch_items bi ON bi.id = bs.batch_item_id
                  WHERE bi.batch_id = {stage.BatchId}) s
            WHERE id = {stage.BatchId}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(int Min, int Max)?> GetRecipeStageDurationsAsync(Guid recipeStageId, CancellationToken cancellationToken)
    {
        var stage = await db.RecipeStages.AsNoTracking().Where(s => s.Id == recipeStageId)
            .Select(s => new { s.MinDurationMinutes, s.MaxDurationMinutes }).SingleOrDefaultAsync(cancellationToken);
        return stage is null ? null : (stage.MinDurationMinutes, stage.MaxDurationMinutes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KneadingItem>?> GetKneadingItemsAsync(Guid ownerId, Guid batchId, CancellationToken cancellationToken)
    {
        if (!await db.Batches.AsNoTracking().AnyAsync(b => b.OwnerId == ownerId && b.Id == batchId, cancellationToken))
        {
            return null;
        }

        return await db.BatchItems.AsNoTracking().Where(i => i.BatchId == batchId).OrderBy(i => i.SortOrder)
            .Select(i => new KneadingItem(i.Id, i.RecipeId, i.Recipe.Title, i.Loaves, i.KneadingCycles))
            .ToListAsync(cancellationToken);
    }

    private static void ApplyHeader(Batch entity, NewBatch batch)
    {
        entity.Name = batch.Name;
        entity.Source = batch.Source;
        entity.PlannedStartAt = batch.PlannedStartAt.UtcDateTime;
        entity.PriceDate = batch.PriceDate;
        entity.Currency = batch.Currency;
        entity.Notes = batch.Notes;
    }

    private static BatchItem ToItem(BatchItemRequest item, int index) => new()
    {
        RecipeId = item.RecipeId,
        Loaves = item.Loaves,
        StarterId = item.StarterId,
        StarterReminderMinutes = item.StarterReminderMinutes,
        SortOrder = index + 1,
    };

    private static BatchDto ToDto(Batch b, Guid? bakeRecordId) => new(
        b.Id, b.Name, b.Status, b.Source, Utc(b.PlannedStartAt), Utc(b.PlannedReadyFrom), Utc(b.PlannedReadyTo),
        Utc(b.ActualStartAt), Utc(b.CompletedAt), b.PriceDate, b.TotalCost, b.Currency.Trim(), b.Notes,
        b.Items.OrderBy(i => i.SortOrder).Select(i => new BatchItemDto(i.Id, i.RecipeId, i.Recipe.Title, i.Loaves, i.StarterId,
            i.StarterAmountG, i.KneadingCycles, i.StarterReminderMinutes, i.SortOrder,
            i.Stages.OrderBy(s => s.SortOrder).Select(s => new BatchStageDto(s.Id, s.SortOrder, s.Name, s.StageTypeCode,
                s.PlannedMinMinutes, s.PlannedMaxMinutes, Utc(s.PlannedStartAt), Utc(s.PlannedEndMinAt), Utc(s.PlannedEndMaxAt),
                Utc(s.ActualStartAt), Utc(s.ActualEndAt), s.Status, s.ExtendedMinutes, s.IsOverridden, s.ActualTemperatureC,
                s.Note)).ToList())).ToList(),
        bakeRecordId);

    /// <summary>A preview is not stored: its ids are cleared so that they cannot be used.</summary>
    private static BatchDto AsPreview(BatchDto planned) => planned with
    {
        Id = Guid.Empty,
        Items = planned.Items.Select(i => i with
        {
            Id = Guid.Empty,
            Stages = i.Stages.Select(s => s with { Id = Guid.Empty }).ToList(),
        }).ToList(),
    };

    internal static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    internal static DateTimeOffset? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
}
