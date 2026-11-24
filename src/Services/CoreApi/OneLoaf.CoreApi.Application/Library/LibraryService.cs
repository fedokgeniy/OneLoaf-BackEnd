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
