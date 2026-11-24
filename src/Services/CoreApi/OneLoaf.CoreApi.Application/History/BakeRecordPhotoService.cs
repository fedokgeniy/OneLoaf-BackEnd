using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.History;

/// <summary>Crumb photos of the own bake records (US-51).</summary>
public sealed class BakeRecordPhotoService(IBakeRecordPhotoRepository photos, IPhotoStorage storage, ICurrentUser currentUser)
{
    private const string RecordNotFoundMessage = "The bake record was not found.";
    private const string PhotoNotFoundMessage = "The photo was not found.";

    /// <summary>Relative URL of a photo.</summary>
    public static string Url(Guid bakeRecordId, Guid photoId) => $"/api/v1/bake-records/{bakeRecordId}/photos/{photoId}";

    /// <summary>Stores an uploaded image; 422 for another format, an empty or too large file, or too many photos.</summary>
    public async Task<BakeRecordPhotoDto> UploadAsync(Guid bakeRecordId, Stream content, long length, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var count = await photos.CountAsync(currentUser.Id, bakeRecordId, cancellationToken)
                    ?? throw new NotFoundException(RecordNotFoundMessage);
        if (count >= PhotoLimits.MaxPerRecord)
        {
            throw new BusinessRuleException($"A bake record can have at most {PhotoLimits.MaxPerRecord} photos.");
        }

        if (length is <= 0 or > PhotoLimits.MaxBytes)
        {
            throw new BusinessRuleException($"The photo must be between 1 byte and {PhotoLimits.MaxBytes / 1024 / 1024} MB.");
        }

        using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, cancellationToken);
        var format = PhotoFormats.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, PhotoFormats.SignatureLength)))
                     ?? throw new BusinessRuleException("Only JPEG, PNG and WebP images are accepted.");

        var photoId = Guid.NewGuid();
        var key = $"{currentUser.Id:N}/{bakeRecordId:N}/{photoId:N}.{format.Extension}";
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, cancellationToken);
        var row = await photos.AddAsync(bakeRecordId, photoId, key, format.ContentType, cancellationToken);
        return ToDto(bakeRecordId, row);
    }

    /// <summary>Photos of an own record.</summary>
    public async Task<IReadOnlyList<BakeRecordPhotoDto>> ListAsync(Guid bakeRecordId, CancellationToken cancellationToken)
    {
        _ = await photos.CountAsync(currentUser.Id, bakeRecordId, cancellationToken) ?? throw new NotFoundException(RecordNotFoundMessage);
        return (await photos.ListAsync(currentUser.Id, bakeRecordId, cancellationToken)).Select(p => ToDto(bakeRecordId, p)).ToList();
    }

    /// <summary>The image of an own photo.</summary>
    public async Task<PhotoContent> OpenAsync(Guid bakeRecordId, Guid photoId, CancellationToken cancellationToken)
    {
        var row = await photos.GetAsync(currentUser.Id, bakeRecordId, photoId, cancellationToken)
                  ?? throw new NotFoundException(PhotoNotFoundMessage);
        var stream = await storage.OpenReadAsync(row.StorageKey, cancellationToken) ?? throw new NotFoundException(PhotoNotFoundMessage);
        return new PhotoContent(stream, row.ContentType);
    }

    /// <summary>Deletes an own photo and its file.</summary>
    public async Task DeleteAsync(Guid bakeRecordId, Guid photoId, CancellationToken cancellationToken)
    {
        var row = await photos.GetAsync(currentUser.Id, bakeRecordId, photoId, cancellationToken)
                  ?? throw new NotFoundException(PhotoNotFoundMessage);
        await photos.DeleteAsync(photoId, cancellationToken);
        await storage.DeleteAsync(row.StorageKey, cancellationToken);
    }

    private static BakeRecordPhotoDto ToDto(Guid bakeRecordId, PhotoRow row) =>
        new(row.Id, row.ContentType, Url(bakeRecordId, row.Id), row.CreatedAt);
}
