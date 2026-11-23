using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Batches and stages, starters and feedings, baking history (US-20 - US-33, US-36).</summary>
public static class ProductionEndpoints
{
    /// <summary>Route of the batches.</summary>
    public const string BatchesRoute = Routes.ApiV1 + "/batches";

    /// <summary>Route of the starters.</summary>
    public const string StartersRoute = Routes.ApiV1 + "/starters";

    /// <summary>Route of the baking history.</summary>
    public const string BakeRecordsRoute = Routes.ApiV1 + "/bake-records";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapProductionEndpoints(this IEndpointRouteBuilder app)
    {
        MapBatches(app);
        MapStarters(app);
        MapBakeRecords(app);
        return app;
    }

    private static void MapBatches(IEndpointRouteBuilder app)
    {
        var batches = app.MapGroup(BatchesRoute).WithTags(ApiTags.Production).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        batches.MapGet("/", async ([AsParameters] BatchListQuery query, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListAsync(query, ct)))
            .WithValidation<BatchListQuery>()
            .WithName("ListBatches").WithSummary("Production calendar")
            .WithDescription("US-26. Batches by planned start, optionally filtered by period and status; paged.");
        batches.MapPost("/preview", async (BatchRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.PreviewAsync(request, ct)))
            .WithValidation<BatchRequest>()
            .WithName("PreviewBatch").WithSummary("Plan a batch without saving it")
            .WithDescription("US-34. Same input as create; the plan is built by the database and rolled back. Ids are empty.");
        batches.MapPost("/", CreateBatchAsync)
            .WithValidation<BatchRequest>()
            .WithName("CreateBatch").WithSummary("Create a batch and build its plan")
            .WithDescription("US-20. Stages, kneading cycles, starter amounts, reminders, ready window and cost from sp_build_batch_plan.");
        batches.MapGet("/{id:guid}", async (Guid id, BatchService s, CancellationToken ct) => TypedResults.Ok(await s.GetAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBatch").WithSummary("Get a batch with items and stages").WithDescription("US-20, US-23.");
        batches.MapPut("/{id:guid}", async (Guid id, BatchRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ReplanAsync(id, request, ct)))
            .WithValidation<BatchRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ReplanBatch").WithSummary("Re-plan a planned batch")
            .WithDescription("US-20. Only a batch that has not started can be changed (409 otherwise).");
        batches.MapPost("/{id:guid}/cancel", CancelBatchAsync)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CancelBatch").WithSummary("Cancel a batch").WithDescription("Scheduled reminders of the batch are cancelled.");
        batches.MapPost("/{id:guid}/complete", async (Guid id, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.CompleteAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CompleteBatch").WithSummary("Complete a running batch")
            .WithDescription("US-31. The database creates the history record with the cost on the bake date.");
        batches.MapGet("/{id:guid}/kneading-plan", async (Guid id, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.GetKneadingPlanAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetKneadingPlan").WithSummary("Merged kneads of the same dough").WithDescription("US-22.");

        var stages = batches.MapGroup("/{id:guid}/stages/{stageId:guid}")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        stages.MapPost("/start", async (Guid id, Guid stageId, StageActionRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.StartStageAsync(id, stageId, request, ct)))
            .WithValidation<StageActionRequest>()
            .WithName("StartStage").WithSummary("Start a stage now")
            .WithDescription("US-23. Stores the actual start; the first started stage moves the batch to in_progress.");
        stages.MapPost("/complete", async (Guid id, Guid stageId, StageActionRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.CompleteStageAsync(id, stageId, request, ct)))
            .WithValidation<StageActionRequest>()
            .WithName("CompleteStage").WithSummary("Complete a running stage now").WithDescription("US-23.");
        stages.MapPost("/skip", async (Guid id, Guid stageId, StageActionRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.SkipStageAsync(id, stageId, request, ct)))
            .WithValidation<StageActionRequest>()
            .WithName("SkipStage").WithSummary("Skip a pending stage").WithDescription("US-09, US-23.");
        stages.MapPost("/extend", async (Guid id, Guid stageId, ExtendStageRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ExtendStageAsync(id, stageId, request.Minutes, ct)))
            .WithValidation<ExtendStageRequest>()
            .WithName("ExtendStage").WithSummary("Extend a stage by N minutes")
            .WithDescription("US-24. Following stages, reminders and the ready window shift (sp_extend_batch_stage).");
        stages.MapPatch("/", async (Guid id, Guid stageId, StageDurationRequest request, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ChangeStageDurationsAsync(id, stageId, request, ct)))
            .WithValidation<StageDurationRequest>()
            .WithName("ChangeStageDurations").WithSummary("Change the planned durations for this batch only").WithDescription("US-21.");
        stages.MapPost("/reset", async (Guid id, Guid stageId, BatchService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ResetStageAsync(id, stageId, ct)))
            .WithName("ResetStage").WithSummary("Restore the durations of the recipe stage").WithDescription("US-21.");
    }

    private static void MapStarters(IEndpointRouteBuilder app)
    {
        var starters = app.MapGroup(StartersRoute).WithTags(ApiTags.Starters).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        starters.MapGet("/", async ([AsParameters] PageQuery query, StarterService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListAsync(query, ct)))
            .WithValidation<PageQuery>()
            .WithName("ListStarters").WithSummary("List starters with their schedule")
            .WithDescription("US-27, US-28, US-30. Last feeding, next feeding and expected peak.");
        starters.MapPost("/", CreateStarterAsync).WithValidation<StarterRequest>().ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateStarter").WithSummary("Create a starter").WithDescription("US-27.");
        starters.MapGet("/{id:guid}", async (Guid id, StarterService s, CancellationToken ct) => TypedResults.Ok(await s.GetAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetStarter").WithSummary("Get a starter").WithDescription("US-27.");
        starters.MapPut("/{id:guid}", async (Guid id, StarterRequest request, StarterService s, CancellationToken ct) =>
                TypedResults.Ok(await s.UpdateAsync(id, request, ct)))
            .WithValidation<StarterRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateStarter").WithSummary("Replace a starter").WithDescription("US-27.");
        starters.MapDelete("/{id:guid}", DeleteStarterAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteStarter").WithSummary("Delete a starter").WithDescription("US-27. Batches keep their items without it.");
        starters.MapGet("/{id:guid}/feedings", async (Guid id, [AsParameters] PageQuery query, StarterService s, CancellationToken ct) =>
                TypedResults.Ok(await s.GetFeedingsAsync(id, query, ct)))
            .WithValidation<PageQuery>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListFeedings").WithSummary("Feeding log, newest first").WithDescription("US-28.");
        starters.MapPost("/{id:guid}/feedings", AddFeedingAsync).WithValidation<FeedingRequest>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("AddFeeding").WithSummary("Record a feeding")
            .WithDescription("US-28, US-29, US-30. The next feeding, the peak and the feeding reminder are set by the database.");
    }

    private static void MapBakeRecords(IEndpointRouteBuilder app)
    {
        var records = app.MapGroup(BakeRecordsRoute).WithTags(ApiTags.History).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        records.MapGet("/", async ([AsParameters] BakeRecordListQuery query, BakeRecordService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ListAsync(query, ct)))
            .WithValidation<BakeRecordListQuery>()
            .WithName("ListBakeRecords").WithSummary("Baking history")
            .WithDescription("US-33. Filter by recipe, period and minimum rating; newest first; paged.");
        records.MapGet("/statistics", async (BakeRecordService s, CancellationToken ct) => TypedResults.Ok(await s.GetStatisticsAsync(ct)))
            .WithName("GetBakeStatistics").WithSummary("Statistics per recipe")
            .WithDescription("Bakes, loaves, average rating and cost per loaf (mv_bake_statistics).");
        records.MapGet("/{id:guid}", async (Guid id, BakeRecordService s, CancellationToken ct) => TypedResults.Ok(await s.GetAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBakeRecord").WithSummary("Get a history record").WithDescription("US-31.");
        records.MapPatch("/{id:guid}", async (Guid id, UpdateBakeRecordRequest request, BakeRecordService s, CancellationToken ct) =>
                TypedResults.Ok(await s.UpdateFeedbackAsync(id, request, ct)))
            .WithValidation<UpdateBakeRecordRequest>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RateBake").WithSummary("Set rating and comment").WithDescription("US-32.");
        records.MapGet("/{id:guid}/stage-log", async (Guid id, BakeRecordService s, CancellationToken ct) =>
                TypedResults.Ok(await s.GetStageLogAsync(id, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetStageLog").WithSummary("Planned vs actual stage times")
            .WithDescription("US-36. Start delays and deviations from the planned windows (v_batch_stage_log).");
        records.MapPut("/{id:guid}/analysis", async (Guid id, SaveAnalysisRequest request, BakeRecordService s, CancellationToken ct) =>
                TypedResults.Ok(await s.SaveAnalysisAsync(id, request, ct)))
            .WithValidation<SaveAnalysisRequest>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("SaveBakeAnalysis").WithSummary("Store the assistant's analysis")
            .WithDescription("US-36. Called by the AI Assistant with the user's token.");
    }

    private static async Task<Created<BatchDto>> CreateBatchAsync(BatchRequest request, BatchService batches, CancellationToken ct)
    {
        var batch = await batches.CreateAsync(request, ct);
        return TypedResults.Created($"{BatchesRoute}/{batch.Id}", batch);
    }

    private static async Task<NoContent> CancelBatchAsync(Guid id, BatchService batches, CancellationToken ct)
    {
        await batches.CancelAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<StarterDto>> CreateStarterAsync(StarterRequest request, StarterService starters, CancellationToken ct)
    {
        var starter = await starters.CreateAsync(request, ct);
        return TypedResults.Created($"{StartersRoute}/{starter.Id}", starter);
    }

    private static async Task<NoContent> DeleteStarterAsync(Guid id, StarterService starters, CancellationToken ct)
    {
        await starters.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Created<FeedingDto>> AddFeedingAsync(Guid id, FeedingRequest request, StarterService starters, CancellationToken ct)
    {
        var feeding = await starters.AddFeedingAsync(id, request, ct);
        return TypedResults.Created($"{StartersRoute}/{id}/feedings", feeding);
    }
}
