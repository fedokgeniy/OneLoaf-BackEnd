using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Starters;

/// <summary>Data access for starters and feedings; scoped to the owner.</summary>
public interface IStarterRepository
{
    /// <summary>Page of starters ordered by name.</summary>
    Task<PagedResult<StarterDto>> ListAsync(Guid ownerId, PageQuery query, CancellationToken cancellationToken);

    /// <summary>The starter, or null.</summary>
    Task<StarterDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the starter exists for the owner.</summary>
    Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the name is taken by another starter of the user.</summary>
    Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Creates a starter; returns its id.</summary>
    Task<Guid> CreateAsync(Guid ownerId, StarterRequest starter, CancellationToken cancellationToken);

    /// <summary>Replaces a starter; false when not found.</summary>
    Task<bool> UpdateAsync(Guid ownerId, Guid id, StarterRequest starter, CancellationToken cancellationToken);

    /// <summary>Deletes a starter; false when not found.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Feedings newest first; null when the starter is not found.</summary>
    Task<PagedResult<FeedingDto>?> GetFeedingsAsync(Guid ownerId, Guid starterId, PageQuery query, CancellationToken cancellationToken);

    /// <summary>Inserts a feeding (the database computes the schedule and replaces the reminder) and returns it.</summary>
    Task<FeedingDto> AddFeedingAsync(Guid starterId, FeedingRequest feeding, CancellationToken cancellationToken);
}
