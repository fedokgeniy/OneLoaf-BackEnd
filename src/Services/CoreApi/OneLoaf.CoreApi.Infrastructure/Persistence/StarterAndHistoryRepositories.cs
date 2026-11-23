using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;
using static OneLoaf.CoreApi.Infrastructure.Persistence.BatchRepository;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IStarterRepository"/>; the schedule comes from the latest feeding.</summary>
public sealed class StarterRepository(OneLoafDbContext db) : IStarterRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<StarterDto>> ListAsync(Guid ownerId, PageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var starters = db.Starters.AsNoTracking().Where(s => s.OwnerId == ownerId);
        var total = await starters.CountAsync(cancellationToken);
        var rows = await Project(starters.OrderBy(s => s.Name).Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<StarterDto>(rows.Select(ToDto).ToList(), query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<StarterDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var row = await Project(db.Starters.AsNoTracking().Where(s => s.OwnerId == ownerId && s.Id == id)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDto(row);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        db.Starters.AsNoTracking().AnyAsync(s => s.OwnerId == ownerId && s.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Starters.AsNoTracking().AnyAsync(s => s.OwnerId == ownerId && s.Name == name && s.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid ownerId, StarterRequest starter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var entity = new Starter { OwnerId = ownerId };
        Apply(entity, starter);
        db.Starters.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, Guid id, StarterRequest starter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var entity = await db.Starters.SingleOrDefaultAsync(s => s.OwnerId == ownerId && s.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        Apply(entity, starter);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Starters.Where(s => s.OwnerId == ownerId && s.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<PagedResult<FeedingDto>?> GetFeedingsAsync(Guid ownerId, Guid starterId, PageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await ExistsAsync(ownerId, starterId, cancellationToken))
        {
            return null;
        }

        var feedings = db.StarterFeedings.AsNoTracking().Where(f => f.StarterId == starterId);
        var total = await feedings.CountAsync(cancellationToken);
        var rows = await feedings.OrderByDescending(f => f.FedAt).Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<FeedingDto>(rows.Select(ToDto).ToList(), query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<FeedingDto> AddFeedingAsync(Guid starterId, FeedingRequest feeding, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feeding);
        var entity = new StarterFeeding
        {
            StarterId = starterId,
            FedAt = feeding.FedAt.UtcDateTime,
            FlourG = feeding.FlourG,
            WaterG = feeding.WaterG,
            StarterKeptG = feeding.StarterKeptG,
            TemperatureC = feeding.TemperatureC,
            Note = string.IsNullOrWhiteSpace(feeding.Note) ? null : feeding.Note.Trim(),
        };
        db.StarterFeedings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        // next_feeding_at and expected_peak_at are computed by a trigger, so the row is read back.
        var stored = await db.StarterFeedings.AsNoTracking().SingleAsync(f => f.Id == entity.Id, cancellationToken);
        return ToDto(stored);
    }

    private static void Apply(Starter entity, StarterRequest starter)
    {
        entity.Name = starter.Name;
        entity.FlourId = starter.FlourId;
        entity.HydrationPercent = starter.HydrationPercent;
        entity.FeedingMode = starter.FeedingMode;
        entity.FeedingIntervalHours = starter.FeedingIntervalHours;
        entity.RiseHours = starter.RiseHours;
        entity.IsActive = starter.IsActive;
        entity.Notes = string.IsNullOrWhiteSpace(starter.Notes) ? null : starter.Notes.Trim();
    }

    private static IQueryable<StarterRow> Project(IQueryable<Starter> starters) =>
        starters.Select(s => new StarterRow(s, s.Feedings.OrderByDescending(f => f.FedAt).FirstOrDefault()));

    private static StarterDto ToDto(StarterRow row) => new(
        row.Starter.Id, row.Starter.Name, row.Starter.FlourId, row.Starter.HydrationPercent, row.Starter.FeedingMode,
        row.Starter.FeedingIntervalHours, row.Starter.RiseHours, row.Starter.IsActive, row.Starter.Notes,
        Utc(row.Last?.FedAt), Utc(row.Last?.NextFeedingAt), Utc(row.Last?.ExpectedPeakAt), Utc(row.Starter.CreatedAt),
        Utc(row.Starter.UpdatedAt));

    private static FeedingDto ToDto(StarterFeeding f) => new(
        f.Id, Utc(f.FedAt), f.FlourG, f.WaterG, f.StarterKeptG, f.TemperatureC, Utc(f.NextFeedingAt), Utc(f.ExpectedPeakAt), f.Note);

    private sealed record StarterRow(Starter Starter, StarterFeeding? Last);
}

/// <summary>EF Core implementation of <see cref="IBakeRecordRepository"/> (records, stage log view, statistics view).</summary>
public sealed class BakeRecordRepository(OneLoafDbContext db) : IBakeRecordRepository
{
    private static readonly Dictionary<string, StageStatus> StageStatuses = Enum.GetValues<StageStatus>()
        .ToDictionary(s => ModelConventions.ToSnakeCase(s.ToString()), StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<PagedResult<BakeRecordSummaryDto>> ListAsync(Guid ownerId, BakeRecordListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var records = db.BakeRecords.AsNoTracking().Where(r => r.OwnerId == ownerId);
        if (query.RecipeId is { } recipeId)
        {
            records = records.Where(r => r.Items.Any(i => i.RecipeId == recipeId));
        }

        if (query.From is { } from)
        {
            records = records.Where(r => r.BakedOn >= from);
        }

        if (query.To is { } to)
        {
            records = records.Where(r => r.BakedOn <= to);
        }

        if (query.MinRating is { } minRating)
        {
            records = records.Where(r => r.Rating >= minRating);
        }

        var total = await records.CountAsync(cancellationToken);
        var rows = await records.OrderByDescending(r => r.BakedOn).ThenByDescending(r => r.CreatedAt)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .Select(r => new
            {
                r.Id, r.BatchId, r.BakedOn, r.TotalLoaves, r.TotalCost, r.Currency, r.Rating,
                HasAnalysis = r.AssistantAnalysis != null,
                Titles = r.Items.Select(i => i.RecipeTitle).ToList(),
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(r => new BakeRecordSummaryDto(r.Id, r.BatchId, r.BakedOn, r.TotalLoaves, r.TotalCost,
            r.Currency.Trim(), r.Rating, r.HasAnalysis, r.Titles)).ToList();
        return new PagedResult<BakeRecordSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<BakeRecordDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var r = await db.BakeRecords.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, cancellationToken);
        return r is null
            ? null
            : new BakeRecordDto(r.Id, r.BatchId, r.BakedOn, r.TotalLoaves, r.TotalCost, r.Currency.Trim(), r.Rating, r.Comment,
                r.AssistantAnalysis, Utc(r.AnalysisAt),
                r.Items.OrderBy(i => i.RecipeTitle).Select(i => new BakeRecordItemDto(i.Id, i.RecipeId, i.RecipeTitle, i.Loaves, i.Cost)).ToList(),
                Utc(r.CreatedAt), Utc(r.UpdatedAt));
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFeedbackAsync(Guid ownerId, Guid id, short? rating, string? comment, CancellationToken cancellationToken) =>
        await db.BakeRecords.Where(r => r.OwnerId == ownerId && r.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Rating, rating)
            .SetProperty(r => r.Comment, comment), cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<bool> SaveAnalysisAsync(Guid ownerId, Guid id, string analysis, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var utc = at.UtcDateTime;
        return await db.BakeRecords.Where(r => r.OwnerId == ownerId && r.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.AssistantAnalysis, analysis)
            .SetProperty(r => r.AnalysisAt, utc), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StageLogEntryDto>?> GetStageLogAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var record = await db.BakeRecords.AsNoTracking().Where(r => r.OwnerId == ownerId && r.Id == id)
            .Select(r => new { r.BatchId }).SingleOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (record.BatchId is not { } batchId)
        {
            return [];
        }

        var rows = await db.Database.SqlQuery<StageLogRow>($"""
            SELECT batch_item_id AS "BatchItemId", recipe_title AS "RecipeTitle", batch_stage_id AS "BatchStageId",
                   sort_order AS "SortOrder", stage_name AS "StageName", stage_type_code AS "StageTypeCode",
                   stage_status AS "StageStatus", planned_start_at AS "PlannedStartAt",
                   planned_end_min_at AS "PlannedEndMinAt", planned_end_max_at AS "PlannedEndMaxAt",
                   actual_start_at AS "ActualStartAt", actual_end_at AS "ActualEndAt",
                   extended_minutes AS "ExtendedMinutes", actual_temperature_c AS "ActualTemperatureC",
                   start_delay_minutes AS "StartDelayMinutes", planned_min_minutes AS "PlannedMinMinutes",
                   planned_max_minutes AS "PlannedMaxMinutes", actual_minutes AS "ActualMinutes",
                   deviation_minutes AS "DeviationMinutes"
            FROM v_batch_stage_log
            WHERE owner_id = {ownerId} AND batch_id = {batchId}
            ORDER BY recipe_title, batch_item_id, sort_order
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new StageLogEntryDto(r.BatchItemId, r.RecipeTitle, r.BatchStageId, r.SortOrder, r.StageName,
            r.StageTypeCode, StageStatuses[r.StageStatus], r.PlannedStartAt, r.PlannedEndMinAt, r.PlannedEndMaxAt, r.ActualStartAt,
            r.ActualEndAt, r.ExtendedMinutes, r.ActualTemperatureC, r.StartDelayMinutes, r.PlannedMinMinutes, r.PlannedMaxMinutes,
            r.ActualMinutes, r.DeviationMinutes)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BakeStatisticsDto>> GetStatisticsAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<BakeStatisticsDto>($"""
            SELECT recipe_id AS "RecipeId", recipe_title AS "RecipeTitle", bakes_count AS "BakesCount",
                   loaves_total AS "LoavesTotal", avg_rating AS "AvgRating", rated_count AS "RatedCount",
                   avg_cost_per_loaf AS "AvgCostPerLoaf", first_baked_on AS "FirstBakedOn", last_baked_on AS "LastBakedOn"
            FROM mv_bake_statistics
            WHERE owner_id = {ownerId}
            ORDER BY last_baked_on DESC, recipe_title
            """).ToListAsync(cancellationToken);

    private sealed class StageLogRow
    {
        public Guid BatchItemId { get; set; }

        public string RecipeTitle { get; set; } = string.Empty;

        public Guid BatchStageId { get; set; }

        public int SortOrder { get; set; }

        public string StageName { get; set; } = string.Empty;

        public string StageTypeCode { get; set; } = string.Empty;

        public string StageStatus { get; set; } = string.Empty;

        public DateTimeOffset PlannedStartAt { get; set; }

        public DateTimeOffset PlannedEndMinAt { get; set; }

        public DateTimeOffset PlannedEndMaxAt { get; set; }

        public DateTimeOffset? ActualStartAt { get; set; }

        public DateTimeOffset? ActualEndAt { get; set; }

        public int ExtendedMinutes { get; set; }

        public decimal? ActualTemperatureC { get; set; }

        public decimal? StartDelayMinutes { get; set; }

        public int PlannedMinMinutes { get; set; }

        public int PlannedMaxMinutes { get; set; }

        public int? ActualMinutes { get; set; }

        public int? DeviationMinutes { get; set; }
    }
}
