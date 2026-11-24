namespace OneLoaf.CoreApi.Application.History;

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
