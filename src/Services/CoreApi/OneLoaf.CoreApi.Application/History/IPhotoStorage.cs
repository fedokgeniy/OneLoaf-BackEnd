namespace OneLoaf.CoreApi.Application.History;

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
