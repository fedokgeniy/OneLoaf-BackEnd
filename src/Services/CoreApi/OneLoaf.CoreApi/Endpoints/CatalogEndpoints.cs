using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Flours, ingredients and price history of the current user (US-10, US-11, US-12).</summary>
public static class CatalogEndpoints
{
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
}
