using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.History;

/// <summary>A crumb photo of a bake record (US-51).</summary>
/// <param name="Id">Photo.</param>
/// <param name="ContentType">image/jpeg, image/png or image/webp.</param>
/// <param name="Url">Relative URL of the image (needs the bearer token).</param>
/// <param name="CreatedAt">Upload time.</param>
public sealed record BakeRecordPhotoDto(Guid Id, string ContentType, string Url, DateTimeOffset CreatedAt);

/// <summary>A photo file to send to the client.</summary>
/// <param name="Content">Image data; the caller disposes it.</param>
/// <param name="ContentType">Media type.</param>
public sealed record PhotoContent(Stream Content, string ContentType);

/// <summary>A stored photo row.</summary>
/// <param name="Id">Photo.</param>
/// <param name="StorageKey">Key of the file in the photo storage.</param>
/// <param name="ContentType">Media type.</param>
/// <param name="CreatedAt">Upload time.</param>
public sealed record PhotoRow(Guid Id, string StorageKey, string ContentType, DateTimeOffset CreatedAt);

/// <summary>The <c>bake_record_photos</c> table; scoped to the owner of the record.</summary>
public interface IBakeRecordPhotoRepository
{
    /// <summary>Photo count of an own record, or null when the record is not the owner's.</summary>
    Task<int?> CountAsync(Guid ownerId, Guid bakeRecordId, CancellationToken cancellationToken);

    /// <summary>Photos of an own record, oldest first.</summary>
    Task<IReadOnlyList<PhotoRow>> ListAsync(Guid ownerId, Guid bakeRecordId, CancellationToken cancellationToken);

    /// <summary>A photo of an own record, or null.</summary>
    Task<PhotoRow?> GetAsync(Guid ownerId, Guid bakeRecordId, Guid photoId, CancellationToken cancellationToken);

    /// <summary>Stores the row.</summary>
    Task<PhotoRow> AddAsync(Guid bakeRecordId, Guid photoId, string storageKey, string contentType, CancellationToken cancellationToken);

    /// <summary>Deletes the row.</summary>
    Task DeleteAsync(Guid photoId, CancellationToken cancellationToken);
}

/// <summary>Where photo files live (a mounted volume; object storage can implement the same port).</summary>
public interface IPhotoStorage
{
    /// <summary>Writes the file.</summary>
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens the file, or null when it is missing.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes the file if it exists.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Limits of photo uploads.</summary>
public static class PhotoLimits
{
    /// <summary>Largest file.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Most photos per bake record.</summary>
    public const int MaxPerRecord = 10;

    /// <summary>Form field of the file.</summary>
    public const string FormField = "file";
}

/// <summary>Image formats accepted for photos, recognised by their file signature (not by the declared type).</summary>
public static class PhotoFormats
{
    /// <summary>Bytes needed to recognise a format.</summary>
    public const int SignatureLength = 12;

    /// <summary>Media type and file extension of the image, or null for anything else.</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ("image/jpeg", "jpg");
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("image/png", "png");
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", "webp");
        }

        return null;
    }
}

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
