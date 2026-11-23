using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

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
/// <param name="Status">Status filter; open by default.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record ReportListQuery(ReportStatus Status = ReportStatus.Open, int Page = 1, int PageSize = Paging.DefaultPageSize);

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

/// <summary>Limits of library requests (database column sizes).</summary>
public static class LibraryLimits
{
    /// <summary>Longest comment.</summary>
    public const int MaxCommentLength = 2000;

    /// <summary>Longest report reason.</summary>
    public const int MaxReportReasonLength = 1000;

    /// <summary>Longest removal reason.</summary>
    public const int MaxRemovedReasonLength = 500;

    /// <summary>Shortest reason.</summary>
    public const int MinReasonLength = 3;

    /// <summary>Longest search text.</summary>
    public const int MaxSearchLength = 200;
}

/// <summary>Validates <see cref="LibraryListQuery"/>.</summary>
public sealed class LibraryListQueryValidator : AbstractValidator<LibraryListQuery>
{
    /// <summary>Creates the rules.</summary>
    public LibraryListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(LibraryLimits.MaxSearchLength);
    }
}

/// <summary>Validates <see cref="CommentListQuery"/>.</summary>
public sealed class CommentListQueryValidator : AbstractValidator<CommentListQuery>
{
    /// <summary>Creates the rules.</summary>
    public CommentListQueryValidator() => this.AddPagingRules(x => x.Page, x => x.PageSize);
}

/// <summary>Validates <see cref="ReportListQuery"/>.</summary>
public sealed class ReportListQueryValidator : AbstractValidator<ReportListQuery>
{
    /// <summary>Creates the rules.</summary>
    public ReportListQueryValidator() => this.AddPagingRules(x => x.Page, x => x.PageSize);
}

/// <summary>Validates <see cref="CommentRequest"/>.</summary>
public sealed class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    /// <summary>Creates the rules.</summary>
    public CommentRequestValidator() =>
        RuleFor(x => x.Body).NotEmpty().Must(b => !string.IsNullOrWhiteSpace(b)).MaximumLength(LibraryLimits.MaxCommentLength);
}

/// <summary>Validates <see cref="ReactionRequest"/>.</summary>
public sealed class ReactionRequestValidator : AbstractValidator<ReactionRequest>
{
    /// <summary>Creates the rules.</summary>
    public ReactionRequestValidator() => RuleFor(x => x.Reaction).IsInEnum();
}

/// <summary>Validates <see cref="ReportRequest"/>.</summary>
public sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    /// <summary>Creates the rules.</summary>
    public ReportRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxReportReasonLength);
}

/// <summary>Validates <see cref="ResolveReportRequest"/>.</summary>
public sealed class ResolveReportRequestValidator : AbstractValidator<ResolveReportRequest>
{
    /// <summary>Creates the rules.</summary>
    public ResolveReportRequestValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxRemovedReasonLength);
    }
}

/// <summary>Validates <see cref="RemovePublicationRequest"/>.</summary>
public sealed class RemovePublicationRequestValidator : AbstractValidator<RemovePublicationRequest>
{
    /// <summary>Creates the rules.</summary>
    public RemovePublicationRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length >= LibraryLimits.MinReasonLength)
            .WithMessage($"reason must have at least {LibraryLimits.MinReasonLength} characters.")
            .MaximumLength(LibraryLimits.MaxRemovedReasonLength);
}

/// <summary>
/// Shared recipe library (US-39 - US-42): publish a snapshot, browse it anonymously, comment, react and copy it into
/// the own workspace.
/// </summary>
public sealed class LibraryService(
    ILibraryRepository library,
    IRecipeRepository recipes,
    IIngredientRepository ingredients,
    RecipeService recipeService,
    ICurrentUser currentUser)
{
    /// <summary>Message of a missing or hidden publication.</summary>
    public const string NotFoundMessage = "Publication not found.";

    private const string CommentNotFoundMessage = "Comment not found.";
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private Guid? ViewerId => currentUser.IsAuthenticated ? currentUser.Id : null;

    /// <summary>Publishes an own recipe as an immutable snapshot (US-39).</summary>
    public async Task<PublishResponse> PublishAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        if (!await recipes.ExistsAsync(currentUser.Id, recipeId, cancellationToken))
        {
            throw new NotFoundException(RecipeService.NotFoundMessage);
        }

        return new PublishResponse(await library.PublishAsync(recipeId, cancellationToken));
    }

    /// <summary>Withdraws an own publication from the catalog (US-39); idempotent.</summary>
    public async Task UnpublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var state = await library.GetStateAsync(id, cancellationToken);
        if (state is null || state.AuthorId != currentUser.Id)
        {
            throw new NotFoundException(NotFoundMessage);
        }

        if (state.Status == PublicationStatus.Removed)
        {
            throw new ConflictException("The publication was removed by a moderator.");
        }

        if (state.Status == PublicationStatus.Published)
        {
            await library.SetStatusAsync(id, PublicationStatus.Unpublished, null, cancellationToken);
        }
    }

    /// <summary>Published recipes (US-40), also for anonymous visitors.</summary>
    public Task<PagedResult<LibraryItemDto>> SearchAsync(LibraryListQuery query, CancellationToken cancellationToken) =>
        library.SearchAsync(query, cancellationToken);

    /// <summary>A publication: published ones for everybody, others only for the author.</summary>
    public async Task<PublishedRecipeDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var publication = await library.GetAsync(id, ViewerId, cancellationToken);
        return publication is not null && (publication.Status == PublicationStatus.Published || publication.IsMine)
            ? publication
            : throw new NotFoundException(NotFoundMessage);
    }

    /// <summary>Visible comments of a published recipe (US-41).</summary>
    public async Task<PagedResult<CommentDto>> ListCommentsAsync(Guid id, CommentListQuery query, CancellationToken cancellationToken)
    {
        await EnsurePublishedAsync(id, cancellationToken);
        return await library.ListCommentsAsync(id, ViewerId, query, cancellationToken);
    }

    /// <summary>Comments a published recipe (US-41).</summary>
    public async Task<CommentDto> AddCommentAsync(Guid id, CommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePublishedAsync(id, cancellationToken);
        return await library.AddCommentAsync(id, currentUser.Id, request.Body.Trim(), cancellationToken);
    }

    /// <summary>Removes an own comment (soft remove).</summary>
    public async Task RemoveCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken)
    {
        if (await library.GetCommentAuthorAsync(id, commentId, cancellationToken) != currentUser.Id)
        {
            throw new NotFoundException(CommentNotFoundMessage);
        }

        await library.RemoveCommentAsync(commentId, cancellationToken);
    }

    /// <summary>Sets the own reaction (US-41).</summary>
    public async Task SetReactionAsync(Guid id, ReactionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePublishedAsync(id, cancellationToken);
        await library.SetReactionAsync(id, currentUser.Id, request.Reaction, cancellationToken);
    }

    /// <summary>Removes the own reaction; idempotent.</summary>
    public async Task RemoveReactionAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsurePublishedAsync(id, cancellationToken);
        await library.RemoveReactionAsync(id, currentUser.Id, cancellationToken);
    }

    /// <summary>Reports a publication or one of its comments (LR-11).</summary>
    public async Task<ReportCreatedResponse> ReportAsync(Guid id, ReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePublishedAsync(id, cancellationToken);
        if (request.CommentId is { } commentId && await library.GetCommentAuthorAsync(id, commentId, cancellationToken) is null)
        {
            throw new NotFoundException(CommentNotFoundMessage);
        }

        // A report targets exactly one object: the comment when given, otherwise the publication.
        var reportId = await library.AddReportAsync(currentUser.Id, request.CommentId is null ? id : null, request.CommentId,
            request.Reason.Trim(), cancellationToken);
        return new ReportCreatedResponse(reportId);
    }

    /// <summary>
    /// Copies a published recipe into the own workspace as an independent recipe (US-42). Ingredients are matched to
    /// the own catalog by name; missing ones are created with the category and unit of the snapshot.
    /// </summary>
    public async Task<RecipeDto> CopyAsync(Guid id, CancellationToken cancellationToken)
    {
        var publication = await library.GetSnapshotAsync(id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        var snapshot = JsonSerializer.Deserialize<RecipeSnapshot>(publication.Snapshot, SnapshotJson)
                       ?? throw new BusinessRuleException("The publication has no recipe content.");

        var names = snapshot.Ingredients.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = new Dictionary<string, Guid>(await library.GetIngredientIdsByNameAsync(currentUser.Id, names, cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        foreach (var ingredient in snapshot.Ingredients.Where(i => !existing.ContainsKey(i.Name)))
        {
            existing[ingredient.Name] = await ingredients.CreateAsync(currentUser.Id,
                new IngredientRequest(ingredient.Name, ingredient.Category, ingredient.Unit, null), cancellationToken);
        }

        var request = new RecipeRequest(
            publication.Title, publication.Description, snapshot.YieldLoaves, snapshot.LoafWeightG, null,
            snapshot.Ingredients.Select(i => new RecipeIngredientInput(null, existing[i.Name], i.Quantity, i.IsOptional)).ToList(),
            snapshot.Stages.Select(s => new RecipeStageInput(null, s.Name, s.StageType, s.MinMinutes, s.MaxMinutes,
                ProofMode: s.ProofMode, TemperatureC: s.TemperatureC, IsOptional: s.IsOptional)).ToList());
        var recipe = await recipeService.CreateAsync(request, cancellationToken);
        await library.MarkCopiedAsync(recipe.Id, id, cancellationToken);
        return recipe;
    }

    private async Task EnsurePublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await library.GetStateAsync(id, cancellationToken) is not { Status: PublicationStatus.Published })
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    private sealed record RecipeSnapshot(
        int YieldLoaves, decimal? LoafWeightG, IReadOnlyList<SnapshotIngredient> Ingredients, IReadOnlyList<SnapshotStage> Stages);

    private sealed record SnapshotIngredient(string Name, string Category, string Unit, decimal Quantity, bool IsOptional);

    private sealed record SnapshotStage(
        string Name, string StageType, int MinMinutes, int MaxMinutes, decimal? TemperatureC, ProofMode? ProofMode, bool IsOptional);
}

/// <summary>Moderation of the library by administrators (LR-11).</summary>
public sealed class ModerationService(ILibraryRepository library, ICurrentUser currentUser)
{
    private const string ReportNotFoundMessage = "Report not found.";

    /// <summary>Reports by status.</summary>
    public Task<PagedResult<ReportDto>> ListReportsAsync(ReportListQuery query, CancellationToken cancellationToken) =>
        library.ListReportsAsync(query, cancellationToken);

    /// <summary>Removes the reported content or rejects the report; 409 when it was already decided.</summary>
    public async Task ResolveAsync(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var report = await library.GetReportStateAsync(reportId, cancellationToken) ?? throw new NotFoundException(ReportNotFoundMessage);
        if (report.Status != ReportStatus.Open)
        {
            throw new ConflictException("The report was already decided.");
        }

        if (request.Action == ModerationAction.Remove)
        {
            if (report.CommentId is { } commentId)
            {
                await library.RemoveCommentAsync(commentId, cancellationToken);
            }
            else if (report.PublishedRecipeId is { } publicationId)
            {
                await library.SetStatusAsync(publicationId, PublicationStatus.Removed, request.Reason.Trim(), cancellationToken);
            }
        }

        var status = request.Action == ModerationAction.Remove ? ReportStatus.Resolved : ReportStatus.Rejected;
        if (!await library.CloseReportAsync(reportId, currentUser.Id, status, cancellationToken))
        {
            throw new ConflictException("The report was already decided.");
        }
    }

    /// <summary>Removes a publication without a report.</summary>
    public async Task RemovePublicationAsync(Guid id, RemovePublicationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = await library.GetStateAsync(id, cancellationToken) ?? throw new NotFoundException(LibraryService.NotFoundMessage);
        await library.SetStatusAsync(id, PublicationStatus.Removed, request.Reason.Trim(), cancellationToken);
    }
}
