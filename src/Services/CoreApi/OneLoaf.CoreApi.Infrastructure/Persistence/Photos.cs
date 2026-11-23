using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

/// <summary>Photo storage settings (<c>Storage__PhotosPath</c>).</summary>
public sealed class PhotoStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storage";

    /// <summary>Directory of the photo files (a Docker volume in production).</summary>
    [Required]
    public string PhotosPath { get; set; } = "/data/photos";
}

/// <summary>Photo files on the local file system (mounted volume). Keys are relative paths built by the service.</summary>
public sealed class LocalPhotoStorage(IOptions<PhotoStorageOptions> options) : IPhotoStorage
{
    /// <inheritdoc />
    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // Keys come from the service (ids and a known extension), but the path is still checked to stay inside the root.
    private string PathOf(string key)
    {
        var root = Path.GetFullPath(options.Value.PhotosPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new InvalidOperationException("Invalid photo key.");
    }
}
