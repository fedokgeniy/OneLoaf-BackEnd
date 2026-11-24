using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.CoreApi.Application.Library;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Security;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Shared recipe library (US-39 - US-42) and its moderation (LR-11).</summary>
public static class LibraryEndpoints
{
    /// <summary>Route of the library.</summary>
    public const string GroupRoute = Routes.ApiV1 + "/library";

    /// <summary>Route of the moderation endpoints.</summary>
    public const string AdminRoute = Routes.ApiV1 + "/admin";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(RecipeEndpoints.GroupRoute + "/{id:guid}/publish", PublishAsync)
            .WithTags(ApiTags.Library).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PublishRecipe").WithSummary("Publish a recipe to the library")
            .WithDescription("US-39. Publishes an immutable snapshot of the effective recipe; later edits of the recipe do not change it.");

        var library = app.MapGroup(GroupRoute).WithTags(ApiTags.Library);
        library.MapGet("/", async ([AsParameters] LibraryListQuery query, LibraryService s, CancellationToken ct) =>
                TypedResults.Ok(await s.SearchAsync(query, ct)))
            .AllowAnonymous().WithValidation<LibraryListQuery>()
            .WithName("SearchLibrary").WithSummary("Browse and search the library")
            .WithDescription("US-40. Anonymous. Published recipes whose title contains the search text, newest first; paged and cached for 5 minutes.");
        library.MapGet("/{id:guid}", async (Guid id, LibraryService s, CancellationToken ct) => TypedResults.Ok(await s.GetAsync(id, ct)))
            .AllowAnonymous().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPublication").WithSummary("A published recipe")
            .WithDescription("US-40. Anonymous. Snapshot, author name, comment and reaction counts. Unpublished ones are visible only to the author.");
        library.MapGet("/{id:guid}/photo", async (Guid id, LibraryService s, CancellationToken ct) =>
            {
                var photo = await s.OpenPhotoAsync(id, ct);
                return TypedResults.Stream(photo.Content, photo.ContentType);
            })
            .AllowAnonymous().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPublicationPhoto").WithSummary("The photo of a published recipe")
            .WithDescription("Anonymous. The photo of the recipe the publication was made from; 404 without a photo or for a hidden publication.");
        library.MapGet("/{id:guid}/comments", async (Guid id, [AsParameters] CommentListQuery query, LibraryService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListCommentsAsync(id, query, ct)))
            .AllowAnonymous().WithValidation<CommentListQuery>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListComments").WithSummary("Comments of a published recipe").WithDescription("US-41. Anonymous. Oldest first; paged.");

        var members = library.MapGroup(string.Empty).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound);
        members.MapPost("/{id:guid}/unpublish", UnpublishAsync).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UnpublishRecipe").WithSummary("Withdraw an own publication")
            .WithDescription("US-39. Only the author; idempotent. 409 when a moderator removed it.");
        members.MapPost("/{id:guid}/comments", AddCommentAsync).WithValidation<CommentRequest>()
            .WithName("AddComment").WithSummary("Comment a published recipe").WithDescription("US-41. The comment is shown with the author's name.");
        members.MapDelete("/{id:guid}/comments/{commentId:guid}", RemoveCommentAsync)
            .WithName("RemoveComment").WithSummary("Remove an own comment").WithDescription("US-41. Soft remove; another user's comment gives 404.");
        members.MapPut("/{id:guid}/reactions/me", SetReactionAsync).WithValidation<ReactionRequest>()
            .WithName("SetReaction").WithSummary("Set the own reaction").WithDescription("US-41. like, love or want_to_bake; replaces the previous one.");
        members.MapDelete("/{id:guid}/reactions/me", RemoveReactionAsync)
            .WithName("RemoveReaction").WithSummary("Remove the own reaction").WithDescription("US-41. Idempotent.");
        members.MapPost("/{id:guid}/copy", CopyAsync).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("CopyPublication").WithSummary("Copy a published recipe into the own workspace")
            .WithDescription("US-42. Creates an independent recipe. The optional body says, per ingredient of the published recipe, which own " +
                             "ingredient to use instead (same unit) or how much of a new one is on the shelf; without a choice an " +
                             "ingredient is matched to the own catalog by name or created.");
        members.MapPost("/{id:guid}/reports", ReportAsync).WithValidation<ReportRequest>()
            .WithName("ReportContent").WithSummary("Report a publication or a comment")
            .WithDescription("LR-11. With commentId the comment is reported, otherwise the publication.");

        var admin = app.MapGroup(AdminRoute).WithTags(ApiTags.Moderation).RequireAuthorization(Policies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);
        admin.MapGet("/reports", async ([AsParameters] ReportListQuery query, ModerationService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListReportsAsync(query, ct)))
            .WithValidation<ReportListQuery>()
            .WithName("ListReports").WithSummary("Content reports").WithDescription("LR-11. Administrators only; open reports by default, oldest first.");
        admin.MapPost("/reports/{id:guid}/resolve", ResolveAsync).WithValidation<ResolveReportRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ResolveReport").WithSummary("Decide a report")
            .WithDescription("LR-11. remove hides the reported comment or removes the publication with the reason; reject keeps it. 409 when already decided.");
        admin.MapPost("/library/{id:guid}/remove", RemovePublicationAsync).WithValidation<RemovePublicationRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RemovePublication").WithSummary("Remove a publication").WithDescription("LR-11. The publication cannot be published again.");
        return app;
    }

    private static async Task<Created<PublishResponse>> PublishAsync(Guid id, LibraryService library, CancellationToken ct)
    {
        var published = await library.PublishAsync(id, ct);
        return TypedResults.Created($"{GroupRoute}/{published.PublishedRecipeId}", published);
    }

    private static async Task<NoContent> UnpublishAsync(Guid id, LibraryService library, CancellationToken ct)
    {
        await library.UnpublishAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<CommentDto>> AddCommentAsync(Guid id, CommentRequest request, LibraryService library, CancellationToken ct)
    {
        var comment = await library.AddCommentAsync(id, request, ct);
        return TypedResults.Created($"{GroupRoute}/{id}/comments/{comment.Id}", comment);
    }

    private static async Task<NoContent> RemoveCommentAsync(Guid id, Guid commentId, LibraryService library, CancellationToken ct)
    {
        await library.RemoveCommentAsync(id, commentId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetReactionAsync(Guid id, ReactionRequest request, LibraryService library, CancellationToken ct)
    {
        await library.SetReactionAsync(id, request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveReactionAsync(Guid id, LibraryService library, CancellationToken ct)
    {
        await library.RemoveReactionAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<Application.Recipes.RecipeDto>> CopyAsync(
        Guid id, CopyPublicationRequest? request, LibraryService library, CancellationToken ct)
    {
        var recipe = await library.CopyAsync(id, request, ct);
        return TypedResults.Created($"{RecipeEndpoints.GroupRoute}/{recipe.Id}", recipe);
    }

    private static async Task<Created<ReportCreatedResponse>> ReportAsync(Guid id, ReportRequest request, LibraryService library, CancellationToken ct)
    {
        var report = await library.ReportAsync(id, request, ct);
        return TypedResults.Created($"{AdminRoute}/reports/{report.ReportId}", report);
    }

    private static async Task<NoContent> ResolveAsync(Guid id, ResolveReportRequest request, ModerationService moderation, CancellationToken ct)
    {
        await moderation.ResolveAsync(id, request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemovePublicationAsync(Guid id, RemovePublicationRequest request, ModerationService moderation, CancellationToken ct)
    {
        await moderation.RemovePublicationAsync(id, request, ct);
        return TypedResults.NoContent();
    }
}
