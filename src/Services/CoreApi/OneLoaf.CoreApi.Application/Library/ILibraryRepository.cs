using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Library;

/// <summary>Publications, comments, reactions and reports.</summary>
public interface ILibraryRepository
{
    /// <summary>Publishes the effective recipe (<c>CALL sp_publish_recipe</c>); returns the publication id.</summary>
    Task<Guid> PublishAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Published recipes matching the title part, newest first.</summary>
    Task<PagedResult<LibraryItemDto>> SearchAsync(LibraryListQuery query, CancellationToken cancellationToken);

    /// <summary>The publication; null when missing.</summary>
    Task<PublishedRecipeDto?> GetAsync(Guid id, Guid? viewerId, CancellationToken cancellationToken);

    /// <summary>Author and status; null when missing.</summary>
    Task<PublicationState?> GetStateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sets the status (unpublished or removed with a reason).</summary>
    Task SetStatusAsync(Guid id, PublicationStatus status, string? removedReason, CancellationToken cancellationToken);

    /// <summary>Visible comments, oldest first.</summary>
    Task<PagedResult<CommentDto>> ListCommentsAsync(Guid publicationId, Guid? viewerId, CommentListQuery query, CancellationToken cancellationToken);

    /// <summary>Adds a comment.</summary>
    Task<CommentDto> AddCommentAsync(Guid publicationId, Guid authorId, string body, CancellationToken cancellationToken);

    /// <summary>Author of a visible comment of the publication; null when missing.</summary>
    Task<Guid?> GetCommentAuthorAsync(Guid publicationId, Guid commentId, CancellationToken cancellationToken);

    /// <summary>Hides a comment (soft remove).</summary>
    Task RemoveCommentAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>Sets or replaces the user's reaction.</summary>
    Task SetReactionAsync(Guid publicationId, Guid userId, ReactionType reaction, CancellationToken cancellationToken);

    /// <summary>Removes the user's reaction.</summary>
    Task RemoveReactionAsync(Guid publicationId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores a report.</summary>
    Task<Guid> AddReportAsync(Guid reporterId, Guid? publicationId, Guid? commentId, string reason, CancellationToken cancellationToken);

    /// <summary>Reports by status, oldest first.</summary>
    Task<PagedResult<ReportDto>> ListReportsAsync(ReportListQuery query, CancellationToken cancellationToken);

    /// <summary>Target and status of a report; null when missing.</summary>
    Task<ReportState?> GetReportStateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Closes an open report; false when it was no longer open.</summary>
    Task<bool> CloseReportAsync(Guid id, Guid adminId, ReportStatus status, CancellationToken cancellationToken);

    /// <summary>Snapshot of a published publication; null when missing or not published.</summary>
    Task<PublicationSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user's ingredients by name (case-insensitive).</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetIngredientIdsByNameAsync(Guid ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    /// <summary>Links a copied recipe to its publication.</summary>
    Task MarkCopiedAsync(Guid recipeId, Guid publicationId, CancellationToken cancellationToken);
}
