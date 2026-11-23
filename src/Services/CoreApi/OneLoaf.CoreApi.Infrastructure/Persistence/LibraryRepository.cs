using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Library;
using OneLoaf.CoreApi.Infrastructure.Caching;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>
/// Library tables (<c>published_recipes</c>, <c>recipe_comments</c>, <c>recipe_reactions</c>, <c>content_reports</c>).
/// Publishing calls <c>sp_publish_recipe</c>; the catalog reads <c>v_public_catalog</c> through the cache (tag
/// <c>library</c>, removed on every status change).
/// </summary>
public sealed class LibraryRepository(OneLoafDbContext db, HybridCache cache, CacheMetrics metrics) : ILibraryRepository
{
    private const string CacheArea = "library";

    /// <inheritdoc />
    public async Task<Guid> PublishAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand("CALL sp_publish_recipe(@recipe_id, NULL::uuid)", connection);
            command.Parameters.AddWithValue("recipe_id", recipeId);
            var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
            await cache.RemoveByTagAsync(CacheKeys.LibraryTag, cancellationToken);
            return id;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public Task<PagedResult<LibraryItemDto>> SearchAsync(LibraryListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = query.Search?.Trim() ?? string.Empty;
        return metrics.GetOrCreateAsync(cache, CacheKeys.LibraryPage(search, query.Page, query.PageSize), CacheArea,
            ct => LoadPageAsync(search, query.Page, query.PageSize, ct), CacheLifetimes.Library, [CacheKeys.LibraryTag], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PublishedRecipeDto?> GetAsync(Guid id, Guid? viewerId, CancellationToken cancellationToken)
    {
        var row = await db.PublishedRecipes.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Title, p.Description, p.Author.DisplayName, p.AuthorId, p.Status, p.PublishedAt, p.Snapshot,
                Comments = p.Comments.Count(c => !c.IsRemoved),
                MyReaction = p.Reactions.Where(r => r.UserId == viewerId).Select(r => (ReactionType?)r.Reaction).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var reactions = await db.RecipeReactions.AsNoTracking()
            .Where(r => r.PublishedRecipeId == id)
            .GroupBy(r => r.Reaction)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        using var snapshot = JsonDocument.Parse(row.Snapshot);
        return new PublishedRecipeDto(row.Id, row.Title, row.Description, row.DisplayName, row.Status, Utc(row.PublishedAt),
            snapshot.RootElement.Clone(), row.Comments, reactions, row.MyReaction, row.AuthorId == viewerId);
    }

    /// <inheritdoc />
    public Task<PublicationState?> GetStateAsync(Guid id, CancellationToken cancellationToken) =>
        db.PublishedRecipes.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PublicationState(p.AuthorId, p.Status))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SetStatusAsync(Guid id, PublicationStatus status, string? removedReason, CancellationToken cancellationToken)
    {
        await db.PublishedRecipes
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.RemovedReason, p => status == PublicationStatus.Removed ? removedReason : p.RemovedReason), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.LibraryTag, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CommentDto>> ListCommentsAsync(
        Guid publicationId, Guid? viewerId, CommentListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var comments = db.RecipeComments.AsNoTracking().Where(c => c.PublishedRecipeId == publicationId && !c.IsRemoved);
        var total = await comments.CountAsync(cancellationToken);
        var items = await comments
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .Select(c => new { c.Id, c.Author.DisplayName, c.Body, c.CreatedAt, c.EditedAt, c.AuthorId })
            .ToListAsync(cancellationToken);
        return new PagedResult<CommentDto>(
            items.Select(c => new CommentDto(c.Id, c.DisplayName, c.Body, Utc(c.CreatedAt), c.EditedAt is { } edited ? Utc(edited) : null,
                c.AuthorId == viewerId)).ToList(),
            query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<CommentDto> AddCommentAsync(Guid publicationId, Guid authorId, string body, CancellationToken cancellationToken)
    {
        var comment = new RecipeComment { PublishedRecipeId = publicationId, AuthorId = authorId, Body = body };
        db.RecipeComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        var created = await db.RecipeComments.AsNoTracking()
            .Where(c => c.Id == comment.Id)
            .Select(c => new { c.Author.DisplayName, c.CreatedAt })
            .SingleAsync(cancellationToken);
        return new CommentDto(comment.Id, created.DisplayName, body, Utc(created.CreatedAt), null, true);
    }

    /// <inheritdoc />
    public Task<Guid?> GetCommentAuthorAsync(Guid publicationId, Guid commentId, CancellationToken cancellationToken) =>
        db.RecipeComments.AsNoTracking()
            .Where(c => c.Id == commentId && c.PublishedRecipeId == publicationId && !c.IsRemoved)
            .Select(c => (Guid?)c.AuthorId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task RemoveCommentAsync(Guid commentId, CancellationToken cancellationToken) =>
        db.RecipeComments.Where(c => c.Id == commentId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsRemoved, true), cancellationToken);

    /// <inheritdoc />
    public Task SetReactionAsync(Guid publicationId, Guid userId, ReactionType reaction, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO recipe_reactions (published_recipe_id, user_id, reaction, created_at)
            VALUES ({publicationId}, {userId}, {ModelConventions.ToSnakeCase(reaction.ToString())}, now())
            ON CONFLICT (published_recipe_id, user_id) DO UPDATE SET reaction = EXCLUDED.reaction
            """, cancellationToken);

    /// <inheritdoc />
    public Task RemoveReactionAsync(Guid publicationId, Guid userId, CancellationToken cancellationToken) =>
        db.RecipeReactions.Where(r => r.PublishedRecipeId == publicationId && r.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> AddReportAsync(Guid reporterId, Guid? publicationId, Guid? commentId, string reason, CancellationToken cancellationToken)
    {
        var report = new ContentReport { ReporterId = reporterId, PublishedRecipeId = publicationId, CommentId = commentId, Reason = reason };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return report.Id;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReportDto>> ListReportsAsync(ReportListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var reports = db.ContentReports.AsNoTracking().Where(r => r.Status == query.Status);
        var total = await reports.CountAsync(cancellationToken);
        var items = await reports
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                PublicationId = r.PublishedRecipeId ?? (r.Comment != null ? r.Comment.PublishedRecipeId : (Guid?)null),
                Title = r.PublishedRecipe != null ? r.PublishedRecipe.Title : r.Comment != null ? r.Comment.PublishedRecipe.Title : null,
                r.CommentId,
                CommentBody = r.Comment != null ? r.Comment.Body : null,
                r.Reason, r.Status, r.CreatedAt, r.ResolvedAt,
            })
            .ToListAsync(cancellationToken);
        return new PagedResult<ReportDto>(
            items.Select(r => new ReportDto(r.Id, r.PublicationId, r.Title, r.CommentId, r.CommentBody, r.Reason, r.Status,
                Utc(r.CreatedAt), r.ResolvedAt is { } resolved ? Utc(resolved) : null)).ToList(),
            query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public Task<ReportState?> GetReportStateAsync(Guid id, CancellationToken cancellationToken) =>
        db.ContentReports.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReportState(r.PublishedRecipeId, r.CommentId, r.Status))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> CloseReportAsync(Guid id, Guid adminId, ReportStatus status, CancellationToken cancellationToken) =>
        await db.ContentReports
            .Where(r => r.Id == id && r.Status == ReportStatus.Open)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.ResolvedById, adminId), cancellationToken) > 0;

    /// <inheritdoc />
    public Task<PublicationSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken) =>
        db.PublishedRecipes.AsNoTracking()
            .Where(p => p.Id == id && p.Status == PublicationStatus.Published)
            .Select(p => new PublicationSnapshot(p.Title, p.Description, p.Snapshot))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Guid>> GetIngredientIdsByNameAsync(
        Guid ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var lower = names.Select(n => n.ToLowerInvariant()).ToList();
        // ToLower is translated to SQL lower(); the culture overloads are not translatable.
#pragma warning disable CA1304, CA1311
        var rows = await db.Ingredients.AsNoTracking()
            .Where(i => i.OwnerId == ownerId && lower.Contains(i.Name.ToLower()))
            .Select(i => new { i.Name, i.Id })
            .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1311
        return rows
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task MarkCopiedAsync(Guid recipeId, Guid publicationId, CancellationToken cancellationToken) =>
        db.Recipes.Where(r => r.Id == recipeId).ExecuteUpdateAsync(s => s.SetProperty(r => r.CopiedFromPublishedRecipeId, publicationId), cancellationToken);

    private async Task<PagedResult<LibraryItemDto>> LoadPageAsync(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var catalog = db.Database.SqlQuery<CatalogRow>($"""
            SELECT published_recipe_id AS "Id", title AS "Title", description AS "Description", author_name AS "AuthorName",
                   published_at AS "PublishedAt", yield_loaves AS "YieldLoaves", ingredient_count AS "IngredientCount",
                   stage_count AS "StageCount", comment_count AS "CommentCount", reaction_count AS "ReactionCount"
            FROM v_public_catalog
            """);
        if (search.Length > 0)
        {
            var pattern = "%" + search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
            catalog = catalog.Where(r => EF.Functions.ILike(r.Title, pattern));
        }

        var total = await catalog.CountAsync(cancellationToken);
        var rows = await catalog
            .OrderByDescending(r => r.PublishedAt).ThenBy(r => r.Id)
            .Skip(Paging.Skip(page, pageSize)).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<LibraryItemDto>(
            rows.Select(r => new LibraryItemDto(r.Id, r.Title, r.Description, r.AuthorName, r.PublishedAt, r.YieldLoaves,
                r.IngredientCount, r.StageCount, r.CommentCount, r.ReactionCount)).ToList(),
            page, pageSize, total);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record CatalogRow(
        Guid Id, string Title, string? Description, string AuthorName, DateTimeOffset PublishedAt, int? YieldLoaves,
        int IngredientCount, int StageCount, long CommentCount, long ReactionCount);
}
