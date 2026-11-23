using System.Text.Json;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Library;

/// <summary>Query of <c>GET /library</c>.</summary>
/// <param name="Search">Part of the title.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record LibraryListQuery(string? Search = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>A publication in the catalog (<c>v_public_catalog</c>).</summary>
/// <param name="Id">Publication.</param>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="AuthorName">Display name of the author.</param>
/// <param name="PublishedAt">Publication time.</param>
/// <param name="YieldLoaves">Loaves of the recipe yield.</param>
/// <param name="IngredientCount">Ingredients in the snapshot.</param>
/// <param name="StageCount">Stages in the snapshot.</param>
/// <param name="CommentCount">Visible comments.</param>
/// <param name="ReactionCount">Reactions.</param>
public sealed record LibraryItemDto(
    Guid Id, string Title, string? Description, string AuthorName, DateTimeOffset PublishedAt, int? YieldLoaves,
    int IngredientCount, int StageCount, long CommentCount, long ReactionCount);

/// <summary>A publication with its snapshot.</summary>
/// <param name="Id">Publication.</param>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="AuthorName">Display name of the author.</param>
/// <param name="Status">published, unpublished (visible to the author only) or removed.</param>
/// <param name="PublishedAt">Publication time.</param>
/// <param name="Snapshot">Recipe at publication time: yield, ingredients (name, category, unit, quantity) and stages.</param>
/// <param name="CommentCount">Visible comments.</param>
/// <param name="Reactions">Count per reaction.</param>
/// <param name="MyReaction">Reaction of the current user.</param>
/// <param name="IsMine">The current user is the author.</param>
public sealed record PublishedRecipeDto(
    Guid Id, string Title, string? Description, string AuthorName, PublicationStatus Status, DateTimeOffset PublishedAt,
    JsonElement Snapshot, long CommentCount, IReadOnlyDictionary<ReactionType, int> Reactions, ReactionType? MyReaction, bool IsMine);

/// <summary>Response of <c>POST /recipes/{id}/publish</c>.</summary>
/// <param name="PublishedRecipeId">The new publication.</param>
public sealed record PublishResponse(Guid PublishedRecipeId);

/// <summary>A comment.</summary>
/// <param name="Id">Comment.</param>
/// <param name="AuthorName">Display name of the author.</param>
/// <param name="Body">Text.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="EditedAt">Last edit.</param>
/// <param name="IsMine">Written by the current user.</param>
public sealed record CommentDto(Guid Id, string AuthorName, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, bool IsMine);

/// <summary>Query of <c>GET /library/{id}/comments</c>.</summary>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record CommentListQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>Body of <c>POST /library/{id}/comments</c>.</summary>
/// <param name="Body">Text, 1-2000 characters.</param>
public sealed record CommentRequest(string Body);

/// <summary>Body of <c>PUT /library/{id}/reactions/me</c>.</summary>
/// <param name="Reaction">like, love or want_to_bake.</param>
public sealed record ReactionRequest(ReactionType Reaction);

/// <summary>Body of <c>POST /library/{id}/reports</c>.</summary>
/// <param name="Reason">Why the content breaks the rules.</param>
/// <param name="CommentId">The reported comment; null reports the publication itself.</param>
public sealed record ReportRequest(string Reason, Guid? CommentId = null);

/// <summary>Response of <c>POST /library/{id}/reports</c>.</summary>
/// <param name="ReportId">The new report.</param>
public sealed record ReportCreatedResponse(Guid ReportId);

/// <summary>Moderator decision.</summary>
public enum ModerationAction
{
    /// <summary>Remove the reported content.</summary>
    Remove,

    /// <summary>Keep the content; the report is unfounded.</summary>
    Reject,
}

/// <summary>Body of <c>POST /admin/reports/{id}/resolve</c>.</summary>
/// <param name="Action">remove or reject.</param>
/// <param name="Reason">Reason shown on a removed publication.</param>
public sealed record ResolveReportRequest(ModerationAction Action, string Reason);

/// <summary>Body of <c>POST /admin/library/{id}/remove</c>.</summary>
/// <param name="Reason">Reason shown on the removed publication.</param>
public sealed record RemovePublicationRequest(string Reason);

/// <summary>Query of <c>GET /admin/reports</c>.</summary>
/// <param name="Status">Status filter: open (default), resolved or rejected.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record ReportListQuery(string Status = "open", int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>A content report for the moderator.</summary>
/// <param name="Id">Report.</param>
/// <param name="PublishedRecipeId">Reported publication (also set for a comment report).</param>
/// <param name="PublicationTitle">Title of the publication.</param>
/// <param name="CommentId">Reported comment.</param>
/// <param name="CommentBody">Text of the reported comment.</param>
/// <param name="Reason">Reason given by the reporter.</param>
/// <param name="Status">open, resolved or rejected.</param>
/// <param name="CreatedAt">Report time.</param>
/// <param name="ResolvedAt">Decision time.</param>
public sealed record ReportDto(
    Guid Id, Guid? PublishedRecipeId, string? PublicationTitle, Guid? CommentId, string? CommentBody, string Reason,
    ReportStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);

/// <summary>Owner and status of a publication.</summary>
/// <param name="AuthorId">Author.</param>
/// <param name="Status">Status.</param>
public sealed record PublicationState(Guid AuthorId, PublicationStatus Status);

/// <summary>Target and status of a report.</summary>
/// <param name="PublishedRecipeId">Reported publication.</param>
/// <param name="CommentId">Reported comment.</param>
/// <param name="Status">Status.</param>
public sealed record ReportState(Guid? PublishedRecipeId, Guid? CommentId, ReportStatus Status);

/// <summary>A publication snapshot to copy.</summary>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="Snapshot">Snapshot JSON.</param>
public sealed record PublicationSnapshot(string Title, string? Description, string Snapshot);
