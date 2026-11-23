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
