using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IFlourRepository"/>.</summary>
public sealed class FlourRepository(OneLoafDbContext db) : IFlourRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<FlourDto>> ListAsync(Guid ownerId, FlourListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var flours = db.Flours.AsNoTracking().Where(f => f.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            flours = flours.Where(f => EF.Functions.ILike(f.Name, Like(query.Search)));
        }

        var total = await flours.CountAsync(cancellationToken);
        var items = await Project(flours.OrderBy(f => f.Name).Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<FlourDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public Task<FlourDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Project(db.Flours.AsNoTracking().Where(f => f.OwnerId == ownerId && f.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Flours.AsNoTracking().AnyAsync(f => f.OwnerId == ownerId && f.Name == name && f.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid ownerId, FlourRequest flour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flour);
        var entity = new Flour
        {
            OwnerId = ownerId,
            Name = flour.Name,
            FlourTypeCode = flour.FlourTypeCode,
            ProteinPercent = flour.ProteinPercent,
            Grind = flour.Grind,
            Notes = flour.Notes,
        };
        db.Flours.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, Guid id, FlourRequest flour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flour);
        return await db.Flours.Where(f => f.OwnerId == ownerId && f.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.Name, flour.Name)
            .SetProperty(f => f.FlourTypeCode, flour.FlourTypeCode)
            .SetProperty(f => f.ProteinPercent, flour.ProteinPercent)
            .SetProperty(f => f.Grind, flour.Grind)
            .SetProperty(f => f.Notes, flour.Notes), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Flours.Where(f => f.OwnerId == ownerId && f.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    internal static string Like(string search) =>
        "%" + search.Trim().Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal) + "%";

    private static IQueryable<FlourDto> Project(IQueryable<Flour> flours) =>
        flours.Select(f => new FlourDto(f.Id, f.Name, f.FlourTypeCode, f.ProteinPercent, f.Grind, f.Notes,
            new DateTimeOffset(f.CreatedAt, TimeSpan.Zero), new DateTimeOffset(f.UpdatedAt, TimeSpan.Zero)));
}
