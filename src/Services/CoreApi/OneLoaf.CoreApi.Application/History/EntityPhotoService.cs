using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.History;

/// <summary>What a single photo belongs to.</summary>
public enum PhotoOwnerKind
{
    /// <summary>An ingredient: the product or its pack.</summary>
    Ingredient,

    /// <summary>A recipe: the loaf.</summary>
    Recipe,
}

/// <summary>The photo of an ingredient or a recipe; both parts are null when there is none.</summary>
/// <param name="StorageKey">Key in object storage.</param>
/// <param name="ContentType">Media type.</param>
public sealed record PhotoSlot(string? StorageKey, string? ContentType);

/// <summary>Data access for the single photo of an ingredient or a recipe.</summary>
public interface IPhotoSlotRepository
{
    /// <summary>The photo of an owned ingredient or recipe; null when the owner has no such ingredient or recipe.</summary>
    Task<PhotoSlot?> GetAsync(Guid ownerId, PhotoOwnerKind kind, Guid id, CancellationToken cancellationToken);

    /// <summary>Stores or clears the photo.</summary>
    Task SetAsync(PhotoOwnerKind kind, Guid id, PhotoSlot photo, CancellationToken cancellationToken);
}

/// <summary>
/// One photo per ingredient (the product or its pack) and per recipe (the loaf). The image is kept in object storage
/// under the owner's prefix; a new upload replaces the old file.
/// </summary>
public sealed class EntityPhotoService(IPhotoSlotRepository photos, IPhotoStorage storage, ICurrentUser currentUser)
{
    private const string PhotoNotFoundMessage = "The photo was not found.";

    /// <summary>Stores the photo, replacing the previous one. JPEG, PNG or WebP, checked by content, up to 5 MB.</summary>
    public async Task SetAsync(PhotoOwnerKind kind, Guid id, Stream content, long length, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var current = await photos.GetAsync(currentUser.Id, kind, id, cancellationToken) ?? throw new NotFoundException(NotFound(kind));
        if (length is <= 0 or > PhotoLimits.MaxBytes)
        {
            throw new BusinessRuleException($"The photo must be between 1 byte and {PhotoLimits.MaxBytes / 1024 / 1024} MB.");
        }

        using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, cancellationToken);
        var format = PhotoFormats.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, PhotoFormats.SignatureLength)))
                     ?? throw new BusinessRuleException("Only JPEG, PNG and WebP images are accepted.");

        // A new key for every upload, so a cached copy of the old image is never shown for the new one.
        var folder = kind == PhotoOwnerKind.Ingredient ? "ingredients" : "recipes";
        var key = $"{currentUser.Id:N}/{folder}/{id:N}-{Guid.NewGuid():N}.{format.Extension}";
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, cancellationToken);
        await photos.SetAsync(kind, id, new PhotoSlot(key, format.ContentType), cancellationToken);
        if (current.StorageKey is { } oldKey)
        {
            await storage.DeleteAsync(oldKey, cancellationToken);
        }
    }

    /// <summary>The image; 404 when there is no photo.</summary>
    public async Task<PhotoContent> OpenAsync(PhotoOwnerKind kind, Guid id, CancellationToken cancellationToken)
    {
        var photo = await photos.GetAsync(currentUser.Id, kind, id, cancellationToken) ?? throw new NotFoundException(NotFound(kind));
        if (photo is not { StorageKey: { } key, ContentType: { } contentType })
        {
            throw new NotFoundException(PhotoNotFoundMessage);
        }

        var stream = await storage.OpenReadAsync(key, cancellationToken) ?? throw new NotFoundException(PhotoNotFoundMessage);
        return new PhotoContent(stream, contentType);
    }

    /// <summary>Removes the photo; nothing happens when there is none.</summary>
    public async Task DeleteAsync(PhotoOwnerKind kind, Guid id, CancellationToken cancellationToken)
    {
        var photo = await photos.GetAsync(currentUser.Id, kind, id, cancellationToken) ?? throw new NotFoundException(NotFound(kind));
        if (photo.StorageKey is { } key)
        {
            await photos.SetAsync(kind, id, new PhotoSlot(null, null), cancellationToken);
            await storage.DeleteAsync(key, cancellationToken);
        }
    }

    private static string NotFound(PhotoOwnerKind kind) =>
        kind == PhotoOwnerKind.Ingredient ? "The ingredient was not found." : "The recipe was not found.";
}
