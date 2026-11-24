using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.Domain;
using OneLoaf.Infrastructure.Persistence;
using static OneLoaf.CoreApi.Infrastructure.Persistence.BatchRepository;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

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
                r.Id,
                r.BatchId,
                r.BakedOn,
                r.TotalLoaves,
                r.TotalCost,
                r.Currency,
                r.Rating,
                HasAnalysis = r.AssistantAnalysis != null,
                Titles = r.Items.Select(i => i.RecipeTitle).ToList(),
                Photos = r.Photos.Count,
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(r => new BakeRecordSummaryDto(r.Id, r.BatchId, r.BakedOn, r.TotalLoaves, r.TotalCost,
            r.Currency.Trim(), r.Rating, r.HasAnalysis, r.Titles, r.Photos)).ToList();
        return new PagedResult<BakeRecordSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<BakeRecordDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var r = await db.BakeRecords.AsNoTracking().Include(x => x.Items).Include(x => x.Photos)
            .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, cancellationToken);
        return r is null
            ? null
            : new BakeRecordDto(r.Id, r.BatchId, r.BakedOn, r.TotalLoaves, r.TotalCost, r.Currency.Trim(), r.Rating, r.Comment,
                r.AssistantAnalysis, Utc(r.AnalysisAt),
                r.Items.OrderBy(i => i.RecipeTitle).Select(i => new BakeRecordItemDto(i.Id, i.RecipeId, i.RecipeTitle, i.Loaves, i.Cost)).ToList(),
                Utc(r.CreatedAt), Utc(r.UpdatedAt),
                r.Photos.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
                    .Select(p => new BakeRecordPhotoDto(p.Id, p.ContentType, BakeRecordPhotoService.Url(r.Id, p.Id), Utc(p.CreatedAt))).ToList());
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
