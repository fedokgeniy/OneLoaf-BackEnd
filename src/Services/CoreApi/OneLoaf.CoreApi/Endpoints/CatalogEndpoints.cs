using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Flours, ingredients, price history and stock of the current user (US-10, US-11, US-12).</summary>
public static class CatalogEndpoints
{
    // Multipart boundaries and headers around the image.
    private const long PhotoUploadOverheadBytes = 64 * 1024;

    /// <summary>Route of the flours.</summary>
    public const string FloursRoute = Routes.ApiV1 + "/flours";

    /// <summary>Route of the ingredients.</summary>
    public const string IngredientsRoute = Routes.ApiV1 + "/ingredients";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var flours = app.MapGroup(FloursRoute).WithTags(ApiTags.Catalog).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        flours.MapGet("/", ListFloursAsync).WithValidation<FlourListQuery>()
            .WithName("ListFlours").WithSummary("List flours").WithDescription("US-11. Paged, ordered by name.");
        flours.MapPost("/", CreateFlourAsync).WithValidation<FlourRequest>().ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateFlour").WithSummary("Create a flour").WithDescription("US-11. Name is unique within the user.");
        flours.MapGet("/{id:guid}", GetFlourAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetFlour").WithSummary("Get a flour").WithDescription("US-11.");
        flours.MapPut("/{id:guid}", UpdateFlourAsync).WithValidation<FlourRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateFlour").WithSummary("Replace a flour").WithDescription("US-11.");
        flours.MapDelete("/{id:guid}", DeleteFlourAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteFlour").WithSummary("Delete a flour")
            .WithDescription("US-11. Linked ingredients and starters keep existing without the flour.");

        var ingredients = app.MapGroup(IngredientsRoute).WithTags(ApiTags.Catalog).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        ingredients.MapGet("/", ListIngredientsAsync).WithValidation<IngredientListQuery>()
            .WithName("ListIngredients").WithSummary("List ingredients with today's price")
            .WithDescription("US-10. Filter by name part and category; paged.");
        ingredients.MapPost("/", CreateIngredientAsync).WithValidation<IngredientRequest>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateIngredient").WithSummary("Create an ingredient").WithDescription("US-10.");
        ingredients.MapGet("/{id:guid}", GetIngredientAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetIngredient").WithSummary("Get an ingredient").WithDescription("US-10.");
        ingredients.MapPut("/{id:guid}", UpdateIngredientAsync).WithValidation<IngredientRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateIngredient").WithSummary("Replace an ingredient").WithDescription("US-10.");
        ingredients.MapDelete("/{id:guid}", DeleteIngredientAsync)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("DeleteIngredient").WithSummary("Delete an ingredient")
            .WithDescription("US-10. An ingredient used in a recipe cannot be deleted (409).");
        ingredients.MapGet("/{id:guid}/prices", GetPricesAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetIngredientPrices").WithSummary("Price history")
            .WithDescription("US-12. Newest first, with the change against the previous price.");
        ingredients.MapPost("/{id:guid}/prices", AddPriceAsync).WithValidation<PriceRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("AddIngredientPrice").WithSummary("Add a price with an effective date")
            .WithDescription("US-12. Older prices are kept; a second price on the same date is a 409.");
        ingredients.MapPut("/{id:guid}/prices/{priceId:guid}", UpdatePriceAsync).WithValidation<PriceRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateIngredientPrice").WithSummary("Replace a price")
            .WithDescription("US-12. Corrects a price entered by mistake; another price on the new date is a 409.");
        ingredients.MapDelete("/{id:guid}/prices/{priceId:guid}", DeletePriceAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteIngredientPrice").WithSummary("Delete a price")
            .WithDescription("US-12. Costs on the dates of the deleted price fall back to the previous price.");
        ingredients.MapGet("/{id:guid}/stock-movements", ListMovementsAsync).WithValidation<StockMovementListQuery>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListStockMovements").WithSummary("Stock ledger of an ingredient")
            .WithDescription("Purchases, batch consumption and returns, starter feedings and corrections; newest first, paged.");
        ingredients.MapPost("/{id:guid}/records", AddRecordAsync).WithValidation<IngredientRecordRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("AddIngredientRecord").WithSummary("Record what was bought: price and quantity")
            .WithDescription("Packs of a size bought for an amount: the price of the date is created or replaced and the stock " +
                             "grows; the stock of the ingredient is kept from now on. priceOnly=true records only the price.");
        ingredients.MapPut("/{id:guid}/photo", SetPhotoAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(PhotoLimits.MaxBytes + PhotoUploadOverheadBytes))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("SetIngredientPhoto").WithSummary("Upload the photo of an ingredient or its pack")
            .WithDescription("multipart/form-data with the image in the field 'file'; JPEG, PNG or WebP (checked by content), " +
                             "up to 5 MB. Replaces the previous photo.");
        ingredients.MapGet("/{id:guid}/photo", GetPhotoAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")
            .WithName("GetIngredientPhoto").WithSummary("The photo of an ingredient").WithDescription("Only the owner; 404 without a photo.");
        ingredients.MapDelete("/{id:guid}/photo", DeletePhotoAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteIngredientPhoto").WithSummary("Remove the photo of an ingredient").WithDescription("204 also when there was none.");
        ingredients.MapPost("/{id:guid}/stock-adjustments", AdjustStockAsync).WithValidation<StockAdjustmentRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("AdjustIngredientStock").WithSummary("Correct the stock to the counted quantity")
            .WithDescription("Writes the difference between the counted quantity and the stock to the ledger; a note is required.");
        return app;
    }

    private static async Task<Ok<PagedResult<FlourDto>>> ListFloursAsync(
        [AsParameters] FlourListQuery query, FlourService flours, CancellationToken ct) =>
        TypedResults.Ok(await flours.ListAsync(query, ct));

    private static async Task<Created<FlourDto>> CreateFlourAsync(FlourRequest request, FlourService flours, CancellationToken ct)
    {
        var flour = await flours.CreateAsync(request, ct);
        return TypedResults.Created($"{FloursRoute}/{flour.Id}", flour);
    }

    private static async Task<Ok<FlourDto>> GetFlourAsync(Guid id, FlourService flours, CancellationToken ct) =>
        TypedResults.Ok(await flours.GetAsync(id, ct));

    private static async Task<Ok<FlourDto>> UpdateFlourAsync(Guid id, FlourRequest request, FlourService flours, CancellationToken ct) =>
        TypedResults.Ok(await flours.UpdateAsync(id, request, ct));

    private static async Task<NoContent> DeleteFlourAsync(Guid id, FlourService flours, CancellationToken ct)
    {
        await flours.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<IngredientDto>>> ListIngredientsAsync(
        [AsParameters] IngredientListQuery query, IngredientService ingredients, CancellationToken ct) =>
        TypedResults.Ok(await ingredients.ListAsync(query, ct));

    private static async Task<Created<IngredientDto>> CreateIngredientAsync(
        IngredientRequest request, IngredientService ingredients, CancellationToken ct)
    {
        var ingredient = await ingredients.CreateAsync(request, ct);
        return TypedResults.Created($"{IngredientsRoute}/{ingredient.Id}", ingredient);
    }

    private static async Task<Ok<IngredientDto>> GetIngredientAsync(Guid id, IngredientService ingredients, CancellationToken ct) =>
        TypedResults.Ok(await ingredients.GetAsync(id, ct));

    private static async Task<Ok<IngredientDto>> UpdateIngredientAsync(
        Guid id, IngredientRequest request, IngredientService ingredients, CancellationToken ct) =>
        TypedResults.Ok(await ingredients.UpdateAsync(id, request, ct));

    private static async Task<NoContent> DeleteIngredientAsync(Guid id, IngredientService ingredients, CancellationToken ct)
    {
        await ingredients.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, IngredientService ingredients, CancellationToken ct) =>
        TypedResults.Ok(await ingredients.GetPricesAsync(id, ct));

    private static async Task<Created<PriceDto>> AddPriceAsync(
        Guid id, PriceRequest request, IngredientService ingredients, CancellationToken ct)
    {
        var price = await ingredients.AddPriceAsync(id, request, ct);
        return TypedResults.Created($"{IngredientsRoute}/{id}/prices", price);
    }

    private static async Task<Ok<PriceDto>> UpdatePriceAsync(
        Guid id, Guid priceId, PriceRequest request, IngredientService ingredients, CancellationToken ct) =>
        TypedResults.Ok(await ingredients.UpdatePriceAsync(id, priceId, request, ct));

    private static async Task<NoContent> DeletePriceAsync(Guid id, Guid priceId, IngredientService ingredients, CancellationToken ct)
    {
        await ingredients.DeletePriceAsync(id, priceId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<StockMovementDto>>> ListMovementsAsync(
        Guid id, [AsParameters] StockMovementListQuery query, StockService stock, CancellationToken ct) =>
        TypedResults.Ok(await stock.ListAsync(id, query, ct));

    private static async Task<Ok<IngredientDto>> AddRecordAsync(
        Guid id, IngredientRecordRequest request, StockService stock, CancellationToken ct) =>
        TypedResults.Ok(await stock.AddRecordAsync(id, request, ct));

    private static async Task<NoContent> SetPhotoAsync(Guid id, IFormFile file, EntityPhotoService photos, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        await photos.SetAsync(PhotoOwnerKind.Ingredient, id, content, file.Length, ct);
        return TypedResults.NoContent();
    }

    private static async Task<FileStreamHttpResult> GetPhotoAsync(Guid id, EntityPhotoService photos, CancellationToken ct)
    {
        var photo = await photos.OpenAsync(PhotoOwnerKind.Ingredient, id, ct);
        return TypedResults.Stream(photo.Content, photo.ContentType);
    }

    private static async Task<NoContent> DeletePhotoAsync(Guid id, EntityPhotoService photos, CancellationToken ct)
    {
        await photos.DeleteAsync(PhotoOwnerKind.Ingredient, id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<StockMovementDto>> AdjustStockAsync(
        Guid id, StockAdjustmentRequest request, StockService stock, CancellationToken ct) =>
        TypedResults.Created($"{IngredientsRoute}/{id}/stock-movements", await stock.AdjustAsync(id, request, ct));
}
