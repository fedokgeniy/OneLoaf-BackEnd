using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IPhotoSlotRepository"/>: the photo columns of ingredients and recipes.</summary>
public sealed class PhotoSlotRepository(OneLoafDbContext db) : IPhotoSlotRepository
{
    /// <inheritdoc />
    public async Task<PhotoSlot?> GetAsync(Guid ownerId, PhotoOwnerKind kind, Guid id, CancellationToken cancellationToken) =>
        kind == PhotoOwnerKind.Ingredient
            ? await db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId && i.Id == id)
                .Select(i => new PhotoSlot(i.PhotoKey, i.PhotoContentType)).SingleOrDefaultAsync(cancellationToken)
            : await db.Recipes.AsNoTracking().Where(r => r.OwnerId == ownerId && r.Id == id)
                .Select(r => new PhotoSlot(r.PhotoKey, r.PhotoContentType)).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task SetAsync(PhotoOwnerKind kind, Guid id, PhotoSlot photo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return kind == PhotoOwnerKind.Ingredient
            ? db.Ingredients.Where(i => i.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(i => i.PhotoKey, photo.StorageKey)
                .SetProperty(i => i.PhotoContentType, photo.ContentType), cancellationToken)
            : db.Recipes.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.PhotoKey, photo.StorageKey)
                .SetProperty(r => r.PhotoContentType, photo.ContentType), cancellationToken);
    }
}
