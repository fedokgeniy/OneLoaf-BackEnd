using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.CoreApi.UnitTests.Fakes;
using OneLoaf.Domain;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.Production;

public sealed class ProductionServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeBatchRepository _batches = new();
    private readonly FakeRecipeRepository _recipes = new();
    private readonly FakeStarterRepository _starters = new();
    private readonly FakeKitchenRepository _kitchen = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeCalculations _calculations = new();
    private readonly Guid _recipeId = Guid.NewGuid();

    public ProductionServiceTests() =>
        _recipes.Add(Me, FakeRecipeRepository.Recipe(_recipeId, null, "Loaf", 1));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private BatchService Batches(Guid? user = null)
    {
        var current = new FakeCurrentUser(user ?? Me);
        var kitchen = new KitchenSettingsService(_kitchen, new ReferenceGuard(new FakeReferenceReader()), current);
        return new BatchService(_batches, _recipes, _starters, _calculations, kitchen, _events, new FixedTimeProvider(Now), current,
            NullLogger<BatchService>.Instance);
    }

    private BatchRequest Request(Guid? starterId = null, DateOnly? priceDate = null) =>
        new(" Weekend ", Now.AddDays(1), [new(_recipeId, 3, starterId, true)], priceDate, "  ");

    // ---- Batches ----

    [Fact]
    public async Task US20_Create_PlansWithKitchenCurrencyAndPublishesBatchPlanned()
    {
        var starter = _starters.Add(Me);

        var batch = await Batches().CreateAsync(Request(starter, new DateOnly(2026, 11, 15)), Ct);

        Assert.NotEqual(Guid.Empty, batch.Id);
        Assert.Equal("Weekend", _batches.LastPlanned!.Name);
        Assert.Null(_batches.LastPlanned.Notes);
        Assert.Equal("EUR", _batches.LastPlanned.Currency);
        Assert.Equal(new DateOnly(2026, 11, 15), _batches.LastPlanned.PriceDate);

        // The feeding reminder comes the time to peak of the starter (6 h) before kneading.
        Assert.Equal(360, Assert.Single(_batches.LastPlanned.Items).StarterReminderMinutes);
        var planned = Assert.IsType<BatchPlanned>(Assert.Single(_events.Published));
        Assert.Equal(batch.Id, planned.BatchId);
        Assert.Equal(Now.AddDays(1).AddMinutes(30), planned.FirstReminderAt);
    }

    [Fact]
    public async Task Preview_DoesNotSaveOrPublish_AndDefaultsPriceDateToKitchenToday()
    {
        var preview = await Batches().PreviewAsync(Request(), Ct);

        Assert.Equal(Guid.Empty, preview.Id);
        Assert.Empty(_batches.Batches);
        Assert.Empty(_events.Published);
        Assert.Equal(new DateOnly(2026, 11, 20), _batches.LastPlanned!.PriceDate);
    }

    [Fact]
    public async Task Create_ForeignRecipeOrStarter_ThrowsValidation()
    {
        var foreignStarter = _starters.Add(Other);

        var recipeError = await Assert.ThrowsAsync<ValidationException>(() =>
            Batches().CreateAsync(Request() with { Items = [new(Guid.NewGuid(), 1)] }, Ct));
        var starterError = await Assert.ThrowsAsync<ValidationException>(() => Batches().CreateAsync(Request(foreignStarter), Ct));

        Assert.Equal("items[0].recipeId", Assert.Single(recipeError.Errors).PropertyName);
        Assert.Equal("items[0].starterId", Assert.Single(starterError.Errors).PropertyName);
    }

    [Fact]
    public async Task Replan_OnlyPlannedBatches()
    {
        var planned = _batches.Add(Me, BatchStatus.Planned);
        var running = _batches.Add(Me, BatchStatus.InProgress);
        var service = Batches();

        await service.ReplanAsync(planned, Request(), Ct);

        Assert.Contains("replan", _batches.Calls);
        await Assert.ThrowsAsync<ConflictException>(() => service.ReplanAsync(running, Request(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ReplanAsync(Guid.NewGuid(), Request(), Ct));
    }

    [Fact]
    public async Task Cancel_ActiveBatchesOnly()
    {
        var planned = _batches.Add(Me, BatchStatus.Planned);
        var done = _batches.Add(Me, BatchStatus.Done);
        var service = Batches();

        await service.CancelAsync(planned, ingredientsUsed: false, Ct);

        Assert.Equal(["return-stock", "status:Cancelled"], _batches.Calls);
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(done, ingredientsUsed: true, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelAsync(Guid.NewGuid(), ingredientsUsed: true, Ct));
    }

    [Fact]
    public async Task Delete_RunningBatch_IsRefusedAndUsedIngredientsStayConsumed()
    {
        var running = _batches.Add(Me, BatchStatus.InProgress);
        var done = _batches.Add(Me, BatchStatus.Done);
        var service = Batches();

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(running, ingredientsUsed: false, Ct));
        await service.DeleteAsync(done, ingredientsUsed: true, Ct);

        Assert.Equal(["delete"], _batches.Calls);
    }

    [Fact]
    public async Task US31_Complete_RunningBatch_ReturnsRecordRefreshesAndPublishes()
    {
        var running = _batches.Add(Me, BatchStatus.InProgress);

        var result = await Batches().CompleteAsync(running, Ct);

        Assert.Equal(_batches.BakeRecordId, result.BakeRecordId);
        Assert.Equal(["status:Done", "refresh"], _batches.Calls);
        var completed = Assert.IsType<BatchCompleted>(Assert.Single(_events.Published));
        Assert.Equal(running, completed.BatchId);
    }

    [Fact]
    public async Task Complete_PlannedOrFinished_ThrowsConflictWithReason()
    {
        var planned = _batches.Add(Me, BatchStatus.Planned);
        var cancelled = _batches.Add(Me, BatchStatus.Cancelled);
        var service = Batches();

        var notStarted = await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(planned, Ct));
        var finished = await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(cancelled, Ct));

        Assert.Equal(BatchService.NotStartedMessage, notStarted.Message);
        Assert.Equal(BatchService.NotActiveMessage, finished.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task US23_StageActions_FollowTheStageState()
    {
        var batch = _batches.Add(Me, BatchStatus.InProgress);
        var pending = _batches.AddStage(batch, StageStatus.Pending);
        var running = _batches.AddStage(batch, StageStatus.Running);
        var service = Batches();
        var action = new StageActionRequest(24.5m, "warm kitchen");

        await service.StartStageAsync(batch, pending, action, Ct);
        await service.CompleteStageAsync(batch, running, action, Ct);
        await service.SkipStageAsync(batch, pending, action, Ct);
        await service.ExtendStageAsync(batch, running, 30, Ct);
        await service.ChangeStageDurationsAsync(batch, pending, new StageDurationRequest(15, 25), Ct);

        Assert.Equal(["start", "complete", "skip", "extend:30", "durations:15-25:True"], _batches.Calls);
        await Assert.ThrowsAsync<ConflictException>(() => service.StartStageAsync(batch, running, action, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => service.CompleteStageAsync(batch, pending, action, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => service.SkipStageAsync(batch, running, action, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => service.ChangeStageDurationsAsync(batch, running, new(1, 2), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.StartStageAsync(batch, Guid.NewGuid(), action, Ct));
    }

    [Fact]
    public async Task StageActions_OnFinishedBatch_ThrowConflict()
    {
        var batch = _batches.Add(Me, BatchStatus.Done);
        var stage = _batches.AddStage(batch, StageStatus.Done);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Batches().ExtendStageAsync(batch, stage, 30, Ct));

        Assert.Equal(BatchService.NotActiveMessage, exception.Message);
    }

    [Fact]
    public async Task US21_Reset_UsesRecipeDurationsOrFailsWithoutRecipeStage()
    {
        var batch = _batches.Add(Me, BatchStatus.Planned);
        var linked = _batches.AddStage(batch, StageStatus.Pending, Guid.NewGuid());
        var orphan = _batches.AddStage(batch, StageStatus.Pending);
        var service = Batches();

        await service.ResetStageAsync(batch, linked, Ct);

        Assert.Contains("durations:60-90:False", _batches.Calls);
        await Assert.ThrowsAsync<ConflictException>(() => service.ResetStageAsync(batch, orphan, Ct));
        _batches.RecipeStageDurations = null;
        await Assert.ThrowsAsync<ConflictException>(() => service.ResetStageAsync(batch, linked, Ct));
    }

    [Fact]
    public async Task ListGetAndForeignBatch()
    {
        var mine = _batches.Add(Me, BatchStatus.Planned);
        var theirs = _batches.Add(Other, BatchStatus.Planned);
        var service = Batches();

        Assert.Equal(mine, (await service.GetAsync(mine, Ct)).Id);
        Assert.Equal(1, (await service.ListAsync(new BatchListQuery(), Ct)).TotalCount);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(theirs, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetKneadingPlanAsync(theirs, Ct));
    }

    [Fact]
    public async Task US22_KneadingPlan_GroupsItemsByRecipe()
    {
        var batch = _batches.Add(Me, BatchStatus.Planned);
        var other = Guid.NewGuid();
        _batches.KneadingItems.AddRange(
        [
            new(Guid.NewGuid(), _recipeId, "Loaf", 2, 1),
            new(Guid.NewGuid(), _recipeId, "Loaf", 2, 1),
            new(Guid.NewGuid(), other, "Rye", 1, 1),
        ]);

        var plan = await Batches().GetKneadingPlanAsync(batch, Ct);

        Assert.Equal(2, plan.Groups.Count);
        var loaf = plan.Groups.Single(g => g.RecipeId == _recipeId);
        Assert.Equal(4, loaf.Loaves);
        Assert.Equal(3840m, loaf.DoughWeightG);
        Assert.Equal(2, loaf.SeparateCycles);
        Assert.Equal(1, loaf.MergedCycles);
        Assert.Contains("instead of 2", loaf.Explanation, StringComparison.Ordinal);
        Assert.Equal(3, plan.TotalSeparateCycles);
        Assert.Equal(2, plan.TotalMergedCycles);
    }

    [Fact]
    public void KneadingPlanner_NoBowlOrNoSaving_ExplainsWhy()
    {
        var recipe = Guid.NewGuid();
        var noBowl = KneadingPlanner.Plan(Guid.NewGuid(), [new(Guid.NewGuid(), recipe, "Loaf", 1, null)],
            new Dictionary<Guid, ProductionFigures> { [recipe] = new(960, null, 100) });
        var noSaving = KneadingPlanner.Plan(Guid.NewGuid(),
            [new(Guid.NewGuid(), recipe, "Loaf", 4, 1), new(Guid.NewGuid(), recipe, "Loaf", 4, 1)],
            new Dictionary<Guid, ProductionFigures> { [recipe] = new(7680, 2, 800) });
        var single = KneadingPlanner.Plan(Guid.NewGuid(), [new(Guid.NewGuid(), recipe, "Loaf", 1, 1)],
            new Dictionary<Guid, ProductionFigures> { [recipe] = new(960, 1, 100) });

        Assert.Contains("mixer bowl", noBowl.Groups[0].Explanation, StringComparison.Ordinal);
        Assert.Null(noBowl.TotalMergedCycles);
        Assert.Contains("does not save", noSaving.Groups[0].Explanation, StringComparison.Ordinal);
        Assert.Contains("1 kneading cycle", single.Groups[0].Explanation, StringComparison.Ordinal);
    }

    // ---- Starters ----

    [Fact]
    public async Task US27_US28_StarterLifecycleAndFeeding()
    {
        var flours = new FakeFlourRepository();
        var flourId = await flours.CreateAsync(Me, new FlourRequest("Rye", "rye", null, null, null), Ct);
        var service = new StarterService(_starters, flours, new FakeIngredientRepository(), _events, new FixedTimeProvider(Now), new FakeCurrentUser(Me),
            NullLogger<StarterService>.Instance);

        var created = await service.CreateAsync(new StarterRequest(" Rye ", 100, FeedingMode.Regular, 24, flourId, 12), Ct);
        var updated = await service.UpdateAsync(created.Id, new StarterRequest("Rye mother", 80, FeedingMode.Fridge, 168), Ct);
        var feeding = await service.AddFeedingAsync(created.Id, new FeedingRequest(Now, 50, 50), Ct);
        var log = await service.GetFeedingsAsync(created.Id, new PageQuery(), Ct);
        var list = await service.ListAsync(new PageQuery(), Ct);

        Assert.Equal("Rye", created.Name);
        Assert.Equal("Rye mother", updated.Name);
        Assert.Equal(Now.AddHours(24), feeding.NextFeedingAt);
        Assert.Single(log.Items);
        Assert.Equal(1, list.TotalCount);
        var fed = Assert.IsType<StarterFed>(Assert.Single(_events.Published));
        Assert.Equal(created.Id, fed.StarterId);

        await service.DeleteAsync(created.Id, Ct);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(created.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(created.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddFeedingAsync(created.Id, new FeedingRequest(Now, 1, 1), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetFeedingsAsync(created.Id, new PageQuery(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(created.Id, new StarterRequest("X", 100, FeedingMode.Regular, 24), Ct));
    }

    [Fact]
    public async Task Starter_DuplicateNameOrForeignFlour_Fails()
    {
        _starters.Add(Me, "Rye");
        var flours = new FakeFlourRepository();
        var foreignFlour = await flours.CreateAsync(Other, new FlourRequest("Theirs", "rye", null, null, null), Ct);
        var service = new StarterService(_starters, flours, new FakeIngredientRepository(), _events, TimeProvider.System, new FakeCurrentUser(Me),
            NullLogger<StarterService>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new StarterRequest("Rye", 100, FeedingMode.Regular, 24), Ct));
        var flourError = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new StarterRequest("Wheat", 100, FeedingMode.Regular, 24, foreignFlour), Ct));
        Assert.Equal("flourId", Assert.Single(flourError.Errors).PropertyName);
    }

    // ---- History ----

    [Fact]
    public async Task US32_US36_BakeRecordFeedbackAnalysisLogAndStatistics()
    {
        var records = new FakeBakeRecordRepository();
        var id = records.Add(Me);
        var theirs = records.Add(Other);
        var service = new BakeRecordService(records, new FixedTimeProvider(Now), new FakeCurrentUser(Me));

        var rated = await service.UpdateFeedbackAsync(id, new UpdateBakeRecordRequest(4, "  Good crumb "), Ct);
        var analysed = await service.SaveAnalysisAsync(id, new SaveAnalysisRequest(" Bulk was 40 min longer. "), Ct);
        var cleared = await service.UpdateFeedbackAsync(id, new UpdateBakeRecordRequest(null, " "), Ct);

        Assert.Equal((short)4, rated.Rating);
        Assert.Equal("Good crumb", rated.Comment);
        Assert.Equal("Bulk was 40 min longer.", analysed.AssistantAnalysis);
        Assert.Equal(Now, analysed.AnalysisAt);
        Assert.Null(cleared.Comment);
        Assert.Empty(await service.GetStageLogAsync(id, Ct));
        Assert.Single(await service.GetStatisticsAsync(Ct));
        Assert.Equal(1, (await service.ListAsync(new BakeRecordListQuery(), Ct)).TotalCount);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(theirs, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateFeedbackAsync(theirs, new UpdateBakeRecordRequest(5, null), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAnalysisAsync(theirs, new SaveAnalysisRequest("x"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStageLogAsync(theirs, Ct));
    }

    // ---- Validators ----

    [Fact]
    public void Validators_Rules()
    {
        Assert.True(new BatchRequestValidator().Validate(Request()).IsValid);
        Assert.False(new BatchRequestValidator().Validate(new BatchRequest("", default, [])).IsValid);
        Assert.False(new BatchRequestValidator().Validate(Request() with { Items = [new(Guid.Empty, 0, Guid.Empty, false)] }).IsValid);
        Assert.False(new BatchListQueryValidator().Validate(new BatchListQuery(Now, Now.AddDays(-1))).IsValid);
        Assert.True(new BatchListQueryValidator().Validate(new BatchListQuery(Now, Now.AddDays(7))).IsValid);
        Assert.False(new StageActionRequestValidator().Validate(new StageActionRequest(400, null)).IsValid);
        Assert.False(new ExtendStageRequestValidator().Validate(new ExtendStageRequest(0)).IsValid);
        Assert.True(new ExtendStageRequestValidator().Validate(new ExtendStageRequest()).IsValid);
        Assert.False(new StageDurationRequestValidator().Validate(new StageDurationRequest(20, 10)).IsValid);
        Assert.False(new StarterRequestValidator().Validate(new StarterRequest("", 0, (FeedingMode)9, 0, Guid.Empty, 0)).IsValid);
        Assert.True(new StarterRequestValidator().Validate(new StarterRequest("Rye", 100, FeedingMode.Weekly, 168, null, 12)).IsValid);
        Assert.False(new FeedingRequestValidator().Validate(new FeedingRequest(default, -1, -1, -1, 200)).IsValid);
        Assert.True(new FeedingRequestValidator().Validate(new FeedingRequest(Now, 50, 50)).IsValid);
        Assert.False(new PageQueryValidator().Validate(new PageQuery(0, 0)).IsValid);
        Assert.False(new UpdateBakeRecordRequestValidator().Validate(new UpdateBakeRecordRequest(6, null)).IsValid);
        Assert.True(new UpdateBakeRecordRequestValidator().Validate(new UpdateBakeRecordRequest(5, "ok")).IsValid);
        Assert.False(new SaveAnalysisRequestValidator().Validate(new SaveAnalysisRequest(" ")).IsValid);
        Assert.False(new BakeRecordListQueryValidator().Validate(new BakeRecordListQuery(null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1))).IsValid);
    }
}
