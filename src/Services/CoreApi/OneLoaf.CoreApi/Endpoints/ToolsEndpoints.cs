using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Export;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Oven loading suggestion (US-19) and CSV export (US-50).</summary>
public static class ToolsEndpoints
{
    private const string RecipesFileName = "recipes.csv";
    private const string BakeRecordsFileName = "bake-records.csv";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapToolsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Routes.ApiV1 + "/calculations/oven-loading",
                async (OvenLoadingRequest request, OvenLoadingService s, CancellationToken ct) => TypedResults.Ok(await s.PlanAsync(request, ct)))
            .WithValidation<OvenLoadingRequest>()
            .WithTags(ApiTags.Calculator).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("PlanOvenLoading").WithSummary("Suggest oven rounds")
            .WithDescription("US-19. Splits the loaves into rounds within the oven capacity of the kitchen settings (422 when not set); " +
                             "recipes with the same oven time share a round.");

        var export = app.MapGroup(Routes.ApiV1 + "/export").WithTags(ApiTags.Account).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        export.MapGet("/recipes.csv", async (ExportService s, CancellationToken ct) =>
                TypedResults.File(await s.RecipesCsvAsync(ct), ExportService.CsvContentType, RecipesFileName))
            .Produces(StatusCodes.Status200OK, contentType: ExportService.CsvContentType)
            .WithName("ExportRecipes").WithSummary("Own recipes as CSV").WithDescription("US-50. UTF-8 with BOM, comma separated, ISO 8601 times.");
        export.MapGet("/bake-records.csv", async (ExportService s, CancellationToken ct) =>
                TypedResults.File(await s.BakeRecordsCsvAsync(ct), ExportService.CsvContentType, BakeRecordsFileName))
            .Produces(StatusCodes.Status200OK, contentType: ExportService.CsvContentType)
            .WithName("ExportBakeRecords").WithSummary("Own bake records as CSV").WithDescription("US-50. Newest first.");
        return app;
    }
}
