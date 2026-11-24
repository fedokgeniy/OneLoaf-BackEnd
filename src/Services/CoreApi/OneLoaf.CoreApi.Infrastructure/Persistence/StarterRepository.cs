using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Starters;
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
            // Not chosen: a trigger fills in the usual flour and water of the starter.
            FlourIngredientId = feeding.FlourIngredientId,
            WaterIngredientId = feeding.WaterIngredientId,
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
        entity.UsableShare = starter.UsableShare ?? entity.UsableShare;
        entity.FlourIngredientId = starter.FlourIngredientId;
        entity.WaterIngredientId = starter.WaterIngredientId;
        entity.HydrationPercent = starter.HydrationPercent;
        entity.FeedingMode = starter.FeedingMode;
        entity.FeedingIntervalHours = starter.FeedingIntervalHours;
        entity.RiseHours = starter.RiseHours;
        entity.IsActive = starter.IsActive;
        entity.Notes = string.IsNullOrWhiteSpace(starter.Notes) ? null : starter.Notes.Trim();
    }

    private IQueryable<StarterRow> Project(IQueryable<Starter> starters)
    {
        var now = DateTime.UtcNow;
        return starters.Select(s => new StarterRow(
            s,
            s.Feedings.OrderByDescending(f => f.FedAt).FirstOrDefault(),
            db.StockMovements.Where(m => m.IngredientId == s.IngredientId && (m.AvailableAt == null || m.AvailableAt <= now))
                .Sum(m => (decimal?)m.Quantity) ?? 0m,
            db.StockMovements.Where(m => m.IngredientId == s.IngredientId).Sum(m => (decimal?)m.Quantity) ?? 0m));
    }

    private static StarterDto ToDto(StarterRow row) => new(
        row.Starter.Id, row.Starter.Name, row.Starter.FlourId, row.Starter.HydrationPercent, row.Starter.FeedingMode,
        row.Starter.FeedingIntervalHours, row.Starter.RiseHours, row.Starter.IsActive, row.Starter.Notes,
        Utc(row.Last?.FedAt), Utc(row.Last?.NextFeedingAt), Utc(row.Last?.ExpectedPeakAt), Utc(row.Starter.CreatedAt),
        Utc(row.Starter.UpdatedAt), row.Starter.FlourIngredientId, row.Starter.WaterIngredientId,
        Amount(row.Last), row.Starter.IngredientId, row.Starter.UsableShare, Math.Max(row.UsableNow, 0m), SuggestedKept(row));

    private static decimal? Amount(StarterFeeding? last) =>
        last is null ? null : last.FlourG + last.WaterG + (last.StarterKeptG ?? 0m);

    /// <summary>
    /// What is in the jar for the next feeding: the share that was never stock plus the ripe (or still ripening)
    /// starter that no batch has taken.
    /// </summary>
    private static decimal? SuggestedKept(StarterRow row) =>
        Amount(row.Last) is { } amount
            ? Math.Round(amount * (1m - row.Starter.UsableShare) + Math.Max(row.LedgerTotal, 0m), 1, MidpointRounding.AwayFromZero)
            : null;

    private static FeedingDto ToDto(StarterFeeding f) => new(
        f.Id, Utc(f.FedAt), f.FlourG, f.WaterG, f.StarterKeptG, f.TemperatureC, Utc(f.NextFeedingAt), Utc(f.ExpectedPeakAt), f.Note,
        f.FlourIngredientId, f.WaterIngredientId);

    private sealed record StarterRow(Starter Starter, StarterFeeding? Last, decimal UsableNow, decimal LedgerTotal);
}
