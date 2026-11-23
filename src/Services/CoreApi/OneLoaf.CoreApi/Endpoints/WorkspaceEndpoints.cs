using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Reference data (anonymous), kitchen settings and warnings of the current user.</summary>
public static class WorkspaceEndpoints
{
    /// <summary>Route of the reference data.</summary>
    public const string ReferenceRoute = Routes.ApiV1 + "/reference";

    /// <summary>Route of the kitchen settings.</summary>
    public const string KitchenRoute = Routes.ApiV1 + "/kitchen-settings";

    /// <summary>Route of the warnings.</summary>
    public const string WarningsRoute = Routes.ApiV1 + "/warnings";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var reference = app.MapGroup(ReferenceRoute).WithTags(ApiTags.Reference).AllowAnonymous();
        reference.MapGet("/units", async (IReferenceDataReader r, CancellationToken ct) => TypedResults.Ok(await r.GetUnitsAsync(ct)))
            .WithName("GetUnits").WithSummary("Units of measure").WithDescription("Reference data, fixed by migrations.");
        reference.MapGet("/currencies", async (IReferenceDataReader r, CancellationToken ct) => TypedResults.Ok(await r.GetCurrenciesAsync(ct)))
            .WithName("GetCurrencies").WithSummary("Currencies").WithDescription("Reference data, fixed by migrations.");
        reference.MapGet("/stage-types", async (IReferenceDataReader r, CancellationToken ct) => TypedResults.Ok(await r.GetStageTypesAsync(ct)))
            .WithName("GetStageTypes").WithSummary("Stage types").WithDescription("Reference data with the defaults of new stages.");
        reference.MapGet("/ingredient-categories", async (IReferenceDataReader r, CancellationToken ct) =>
                TypedResults.Ok(await r.GetIngredientCategoriesAsync(ct)))
            .WithName("GetIngredientCategories").WithSummary("Ingredient categories").WithDescription("Reference data.");
        reference.MapGet("/flour-types", async (IReferenceDataReader r, CancellationToken ct) => TypedResults.Ok(await r.GetFlourTypesAsync(ct)))
            .WithName("GetFlourTypes").WithSummary("Flour types").WithDescription("Reference data.");

        var kitchen = app.MapGroup(KitchenRoute).WithTags(ApiTags.Kitchen).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound);
        kitchen.MapGet("/", GetKitchenAsync)
            .WithName("GetKitchenSettings").WithSummary("Get the kitchen settings")
            .WithDescription("US-13, US-47, US-48, US-49. Oven, bowl, tariff, packaging, loss, labor rate, currency, time zone.");
        kitchen.MapPut("/", UpdateKitchenAsync)
            .WithValidation<KitchenSettingsRequest>()
            .WithName("UpdateKitchenSettings").WithSummary("Replace the kitchen settings")
            .WithDescription("US-13, US-47, US-48, US-49. Used by the cost and kneading calculations.");

        app.MapGet(WarningsRoute, GetWarningsAsync)
            .WithTags(ApiTags.Kitchen).RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetWarnings").WithSummary("Data quality warnings")
            .WithDescription("Missing prices, empty recipes, overdue batches and starters of the current user.");
        return app;
    }

    private static async Task<Ok<KitchenSettingsDto>> GetKitchenAsync(KitchenSettingsService kitchen, CancellationToken ct) =>
        TypedResults.Ok(await kitchen.GetAsync(ct));

    private static async Task<Ok<KitchenSettingsDto>> UpdateKitchenAsync(
        KitchenSettingsRequest request, KitchenSettingsService kitchen, CancellationToken ct) =>
        TypedResults.Ok(await kitchen.UpdateAsync(request, ct));

    private static async Task<Ok<IReadOnlyList<WarningDto>>> GetWarningsAsync(WarningsService warnings, CancellationToken ct) =>
        TypedResults.Ok(await warnings.GetAsync(ct));
}
