using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Recipes, variants and the calculator of the current user (US-05 - US-08, US-14 - US-18, US-52).</summary>
public static class RecipeEndpoints
{
    // Multipart boundaries and headers around the image.
    private const long PhotoUploadOverheadBytes = 64 * 1024;

    /// <summary>Route of the recipes.</summary>
    public const string GroupRoute = Routes.ApiV1 + "/recipes";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var recipes = app.MapGroup(GroupRoute).WithTags(ApiTags.Recipes).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        recipes.MapGet("/", ListAsync).WithValidation<RecipeListQuery>()
            .WithName("ListRecipes").WithSummary("List recipes")
            .WithDescription("US-05, US-37. Filter by title part, archived flag and minimum flour protein; paged.");
        recipes.MapPost("/", CreateAsync).WithValidation<RecipeRequest>()
            .WithName("CreateRecipe").WithSummary("Create a recipe with ingredients and stages")
            .WithDescription("US-05, US-09. Saved in one transaction.");
        recipes.MapPost("/drafts", CreateDraftAsync).WithValidation<RecipeDraftRequest>()
            .WithName("CreateRecipeDraft").WithSummary("Create a recipe with ingredients given by name")
            .WithDescription("US-34. Used when a recipe proposed by the assistant is confirmed: ingredients of the user are " +
                             "matched by name, missing ones are created without a price; one transaction.");
        recipes.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetRecipe").WithSummary("Get the effective recipe")
            .WithDescription("US-05, US-07. A variant is shown with the inherited content resolved (isInherited).");
        recipes.MapPut("/{id:guid}", UpdateAsync).WithValidation<RecipeRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("UpdateRecipe").WithSummary("Replace the content of a recipe")
            .WithDescription("US-05, US-07. For a variant only the differences from the parent are stored.");
        recipes.MapDelete("/{id:guid}", DeleteAsync)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("DeleteRecipe").WithSummary("Delete a recipe")
            .WithDescription("US-05. A recipe with variants or used in batches cannot be deleted (409); archive it instead.");
        recipes.MapPut("/{id:guid}/photo", SetPhotoAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(PhotoLimits.MaxBytes + PhotoUploadOverheadBytes))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("SetRecipePhoto").WithSummary("Upload the photo of a recipe")
            .WithDescription("multipart/form-data with the image in the field 'file'; JPEG, PNG or WebP (checked by content), " +
                             "up to 5 MB. Replaces the previous photo. A variant has its own photo.");
        recipes.MapGet("/{id:guid}/photo", GetPhotoAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")
            .WithName("GetRecipePhoto").WithSummary("The photo of a recipe").WithDescription("Only the owner; 404 without a photo.");
        recipes.MapDelete("/{id:guid}/photo", DeletePhotoAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteRecipePhoto").WithSummary("Remove the photo of a recipe").WithDescription("204 also when there was none.");
        recipes.MapPost("/{id:guid}/archive", (Guid id, RecipeService s, CancellationToken ct) => SetArchivedAsync(id, true, s, ct))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ArchiveRecipe").WithSummary("Archive a recipe").WithDescription("US-05. Hidden from the default list.");
        recipes.MapPost("/{id:guid}/unarchive", (Guid id, RecipeService s, CancellationToken ct) => SetArchivedAsync(id, false, s, ct))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UnarchiveRecipe").WithSummary("Restore an archived recipe").WithDescription("US-05.");
        recipes.MapPost("/{id:guid}/variants", CreateVariantAsync).WithValidation<CreateVariantRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("CreateVariant").WithSummary("Create a variant")
            .WithDescription("US-07. The variant inherits everything; change it with PUT.");
        recipes.MapGet("/{id:guid}/comparison", GetComparisonAsync)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("CompareVariant").WithSummary("Compare a variant with its parent")
            .WithDescription("US-08. Unchanged, changed, added and removed lines.");
        recipes.MapGet("/{id:guid}/scaled", GetScaledAsync).WithValidation<LoavesQuery>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ScaleRecipe").WithSummary("Scale to a number of loaves").WithDescription("US-06.");

        var calculator = app.MapGroup(GroupRoute).WithTags(ApiTags.Calculator).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound);
        calculator.MapGet("/{id:guid}/cost", GetCostAsync).WithValidation<CostQuery>()
            .WithName("GetRecipeCost").WithSummary("Cost of N loaves on a price date")
            .WithDescription("US-14, US-15, US-47, US-48, US-49. Lines from fn_recipe_cost; total = sum of the rounded lines.");
        calculator.MapGet("/{id:guid}/timeline", GetTimelineAsync).WithValidation<TimelineQuery>()
            .WithName("GetRecipeTimeline").WithSummary("Stage timeline and ready window for a start time")
            .WithDescription("US-16. From fn_recipe_timeline and fn_recipe_ready_window.");
        calculator.MapGet("/{id:guid}/start-window", GetStartWindowAsync).WithValidation<StartWindowQuery>()
            .WithName("GetRecipeStartWindow").WithSummary("Start window for a desired ready time")
            .WithDescription("US-52. From fn_recipe_start_window.");
        calculator.MapGet("/{id:guid}/production-figures", GetProductionFiguresAsync).WithValidation<LoavesQuery>()
            .WithName("GetProductionFigures").WithSummary("Dough weight, kneading cycles and starter amount")
            .WithDescription("US-17, US-18. From fn_dough_weight_g, fn_kneading_cycles, fn_starter_amount_g.");
        return app;
    }

    private static async Task<NoContent> SetPhotoAsync(Guid id, IFormFile file, EntityPhotoService photos, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        await photos.SetAsync(PhotoOwnerKind.Recipe, id, content, file.Length, ct);
        return TypedResults.NoContent();
    }

    private static async Task<FileStreamHttpResult> GetPhotoAsync(Guid id, EntityPhotoService photos, CancellationToken ct)
    {
        var photo = await photos.OpenAsync(PhotoOwnerKind.Recipe, id, ct);
        return TypedResults.Stream(photo.Content, photo.ContentType);
    }

    private static async Task<NoContent> DeletePhotoAsync(Guid id, EntityPhotoService photos, CancellationToken ct)
    {
        await photos.DeleteAsync(PhotoOwnerKind.Recipe, id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<RecipeSummaryDto>>> ListAsync(
        [AsParameters] RecipeListQuery query, RecipeService recipes, CancellationToken ct) =>
        TypedResults.Ok(await recipes.ListAsync(query, ct));

    private static async Task<Created<RecipeDto>> CreateAsync(RecipeRequest request, RecipeService recipes, CancellationToken ct)
    {
        var recipe = await recipes.CreateAsync(request, ct);
        return TypedResults.Created($"{GroupRoute}/{recipe.Id}", recipe);
    }

    private static async Task<Created<RecipeDraftResponse>> CreateDraftAsync(
        RecipeDraftRequest request, RecipeService recipes, CancellationToken ct)
    {
        var created = await recipes.CreateDraftAsync(request, ct);
        return TypedResults.Created($"{GroupRoute}/{created.Recipe.Id}", created);
    }

    private static async Task<Ok<RecipeDto>> GetAsync(Guid id, RecipeService recipes, CancellationToken ct) =>
        TypedResults.Ok(await recipes.GetAsync(id, ct));

    private static async Task<Ok<RecipeDto>> UpdateAsync(Guid id, RecipeRequest request, RecipeService recipes, CancellationToken ct) =>
        TypedResults.Ok(await recipes.UpdateAsync(id, request, ct));

    private static async Task<NoContent> DeleteAsync(Guid id, RecipeService recipes, CancellationToken ct)
    {
        await recipes.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetArchivedAsync(Guid id, bool archived, RecipeService recipes, CancellationToken ct)
    {
        await recipes.SetArchivedAsync(id, archived, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<RecipeDto>> CreateVariantAsync(
        Guid id, CreateVariantRequest request, RecipeService recipes, CancellationToken ct)
    {
        var variant = await recipes.CreateVariantAsync(id, request, ct);
        return TypedResults.Created($"{GroupRoute}/{variant.Id}", variant);
    }

    private static async Task<Ok<RecipeComparisonDto>> GetComparisonAsync(Guid id, RecipeService recipes, CancellationToken ct) =>
        TypedResults.Ok(await recipes.GetComparisonAsync(id, ct));

    private static async Task<Ok<ScaledRecipeDto>> GetScaledAsync(
        Guid id, [AsParameters] LoavesQuery query, RecipeService recipes, CancellationToken ct) =>
        TypedResults.Ok(await recipes.GetScaledAsync(id, query.Loaves, ct));

    private static async Task<Ok<CostBreakdownDto>> GetCostAsync(
        Guid id, [AsParameters] CostQuery query, CalculationService calculations, CancellationToken ct) =>
        TypedResults.Ok(await calculations.GetCostAsync(id, query, ct));

    private static async Task<Ok<TimelineDto>> GetTimelineAsync(
        Guid id, [AsParameters] TimelineQuery query, CalculationService calculations, CancellationToken ct) =>
        TypedResults.Ok(await calculations.GetTimelineAsync(id, query.Start, ct));

    private static async Task<Ok<StartWindowDto>> GetStartWindowAsync(
        Guid id, [AsParameters] StartWindowQuery query, CalculationService calculations, CancellationToken ct) =>
        TypedResults.Ok(await calculations.GetStartWindowAsync(id, query.ReadyAt, ct));

    private static async Task<Ok<ProductionFiguresDto>> GetProductionFiguresAsync(
        Guid id, [AsParameters] LoavesQuery query, CalculationService calculations, CancellationToken ct) =>
        TypedResults.Ok(await calculations.GetProductionFiguresAsync(id, query.Loaves, ct));
}
