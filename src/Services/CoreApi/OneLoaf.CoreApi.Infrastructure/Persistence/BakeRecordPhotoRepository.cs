using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IBakeRecordPhotoRepository"/>.</summary>
public sealed class BakeRecordPhotoRepository(OneLoafDbContext db) : IBakeRecordPhotoRepository
{
    /// <inheritdoc />
    public async Task<int?> CountAsync(Guid ownerId, Guid bakeRecordId, CancellationToken cancellationToken)
    {
        var record = await db.BakeRecords.AsNoTracking()
            .Where(r => r.Id == bakeRecordId && r.OwnerId == ownerId)
            .Select(r => new { Count = r.Photos.Count })
            .FirstOrDefaultAsync(cancellationToken);
        return record?.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PhotoRow>> ListAsync(Guid ownerId, Guid bakeRecordId, CancellationToken cancellationToken)
    {
        var rows = await Owned(ownerId, bakeRecordId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).ToListAsync(cancellationToken);
        return rows.Select(ToRow).ToList();
    }

    /// <inheritdoc />
    public async Task<PhotoRow?> GetAsync(Guid ownerId, Guid bakeRecordId, Guid photoId, CancellationToken cancellationToken)
    {
        var row = await Owned(ownerId, bakeRecordId).FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        return row is null ? null : ToRow(row);
    }

    /// <inheritdoc />
    public async Task<PhotoRow> AddAsync(Guid bakeRecordId, Guid photoId, string storageKey, string contentType, CancellationToken cancellationToken)
    {
        var photo = new BakeRecordPhoto { Id = photoId, BakeRecordId = bakeRecordId, StorageKey = storageKey, ContentType = contentType };
        db.BakeRecordPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        var createdAt = await db.BakeRecordPhotos.AsNoTracking().Where(p => p.Id == photoId).Select(p => p.CreatedAt).SingleAsync(cancellationToken);
        return new PhotoRow(photoId, storageKey, contentType, new DateTimeOffset(DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)));
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid photoId, CancellationToken cancellationToken) =>
        db.BakeRecordPhotos.Where(p => p.Id == photoId).ExecuteDeleteAsync(cancellationToken);

    private IQueryable<BakeRecordPhoto> Owned(Guid ownerId, Guid bakeRecordId) =>
        db.BakeRecordPhotos.AsNoTracking().Where(p => p.BakeRecordId == bakeRecordId && p.BakeRecord.OwnerId == ownerId);

    private static PhotoRow ToRow(BakeRecordPhoto p) =>
        new(p.Id, p.StorageKey, p.ContentType, new DateTimeOffset(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)));
}
