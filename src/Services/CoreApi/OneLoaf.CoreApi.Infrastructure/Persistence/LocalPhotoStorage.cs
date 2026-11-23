using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using OneLoaf.CoreApi.Application.History;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

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
