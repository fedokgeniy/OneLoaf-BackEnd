namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Publication in the public library list (<c>GET /library</c>, US-40).</summary>
public sealed record LibraryItemDto(
    Guid Id,
    string Title,
    string? Description,
    string AuthorName,
    DateTimeOffset PublishedAt,
    int? YieldLoaves,
    int IngredientCount,
    int StageCount,
    long CommentCount,
    long ReactionCount);

/// <summary>
/// A publication with its recipe snapshot (<c>GET /library/{id}</c>, US-40–42). <see cref="Reactions"/> counts per
/// reaction code (like, love, want_to_bake); <see cref="MyReaction"/> is the signed-in user's own.
/// </summary>
public sealed record PublishedRecipeDto(
    Guid Id,
    string Title,
    string? Description,
    string AuthorName,
    string Status,
    DateTimeOffset PublishedAt,
    PublicationSnapshotDto Snapshot,
    long CommentCount,
    IReadOnlyDictionary<string, int> Reactions,
    string? MyReaction,
    bool IsMine);

/// <summary>Recipe as it was published (schema version 1): no prices, costs or notes.</summary>
public sealed record PublicationSnapshotDto(
    IReadOnlyList<SnapshotIngredientDto> Ingredients,
    IReadOnlyList<SnapshotStageDto> Stages,
    decimal? LoafWeightG,
    int? YieldLoaves,
    int SchemaVersion);

/// <summary>Ingredient line of a snapshot.</summary>
public sealed record SnapshotIngredientDto(string Name, string Unit, string Category, decimal Quantity, bool IsOptional);

/// <summary>Stage of a snapshot.</summary>
public sealed record SnapshotStageDto(
    string Name,
    string? ProofMode,
    string StageType,
    bool IsOptional,
    int MaxMinutes,
    int MinMinutes,
    decimal? TemperatureC);

/// <summary><c>POST /recipes/{id}/publish</c> response (US-39).</summary>
public sealed record PublishResponse(Guid PublishedRecipeId);

/// <summary>Comment under a publication (US-41).</summary>
public sealed record CommentDto(Guid Id, string AuthorName, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, bool IsMine);

/// <summary><c>POST /library/{id}/comments</c> body.</summary>
public sealed record CommentRequest(string Body);

/// <summary><c>PUT /library/{id}/reactions/me</c> body: like, love or want_to_bake.</summary>
public sealed record ReactionRequest(string Reaction);

/// <summary><c>POST /library/{id}/reports</c> body (LR-11): the publication, or one comment of it.</summary>
public sealed record ReportRequest(string Reason, Guid? CommentId);

/// <summary><c>POST /library/{id}/reports</c> response.</summary>
public sealed record ReportCreatedResponse(Guid ReportId);

/// <summary>Report in the moderation queue (<c>GET /admin/reports</c>, LR-11).</summary>
public sealed record ReportDto(
    Guid Id,
    Guid? PublishedRecipeId,
    string? PublicationTitle,
    Guid? CommentId,
    string? CommentBody,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

/// <summary><c>POST /admin/reports/{id}/resolve</c> body: action remove or reject.</summary>
public sealed record ResolveReportRequest(string Action, string Reason);

/// <summary><c>POST /admin/library/{id}/remove</c> body.</summary>
public sealed record RemovePublicationRequest(string Reason);

/// <summary>Reaction codes and moderation actions of the Core API.</summary>
public static class LibraryCodes
{
    /// <summary>Reactions in display order.</summary>
    public static IReadOnlyList<string> Reactions { get; } = ["like", "love", "want_to_bake"];

    /// <summary>Remove the reported content.</summary>
    public const string Remove = "remove";

    /// <summary>Keep the content and close the report.</summary>
    public const string Reject = "reject";
}
