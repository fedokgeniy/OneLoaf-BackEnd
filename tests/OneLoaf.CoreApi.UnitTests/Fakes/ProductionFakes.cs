using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;

namespace OneLoaf.CoreApi.UnitTests.Fakes;

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<IntegrationEvent> Published { get; } = [];

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FakeBatchRepository : IBatchRepository
{
    public Dictionary<Guid, (Guid Owner, BatchDto Batch)> Batches { get; } = [];

    public Dictionary<Guid, StageState> Stages { get; } = [];

    public List<string> Calls { get; } = [];

    public List<KneadingItem> KneadingItems { get; } = [];

    public (int Min, int Max)? RecipeStageDurations { get; set; } = (60, 90);

    public NewBatch? LastPlanned { get; private set; }

    public Guid? BakeRecordId { get; set; } = Guid.NewGuid();

    public Guid Add(Guid owner, BatchStatus status)
    {
        var id = Guid.NewGuid();
        Batches[id] = (owner, Batch(id, status));
        return id;
    }

    public Guid AddStage(Guid batchId, StageStatus status, Guid? recipeStageId = null)
    {
        var id = Guid.NewGuid();
        Stages[id] = new StageState(id, status, Batches[batchId].Batch.Status, recipeStageId, 10, 20);
        return id;
    }

    public Task<PagedResult<BatchSummaryDto>> ListAsync(Guid ownerId, BatchListQuery query, CancellationToken cancellationToken)
    {
        var items = Batches.Values.Where(b => b.Owner == ownerId)
            .Select(b => new BatchSummaryDto(b.Batch.Id, b.Batch.Name, b.Batch.Status, b.Batch.PlannedStartAt, null, null, 0)).ToList();
        return Task.FromResult(new PagedResult<BatchSummaryDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<BatchDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.TryGetValue(id, out var b) && b.Owner == ownerId ? b.Batch : null);

    public Task<BatchDto> PlanAsync(Guid ownerId, NewBatch batch, bool save, CancellationToken cancellationToken)
    {
        LastPlanned = batch;
        var id = save ? Guid.NewGuid() : Guid.Empty;
        var stageStart = batch.PlannedStartAt.AddMinutes(30);
        var dto = Batch(id, BatchStatus.Planned) with
        {
            Name = batch.Name,
            PlannedStartAt = batch.PlannedStartAt,
            PriceDate = batch.PriceDate,
            Items =
            [
                new(Guid.NewGuid(), batch.Items[0].RecipeId, "Loaf", batch.Items[0].Loaves, null, 100, 1, null, 1,
                [
                    new(Guid.NewGuid(), 1, "Mix", "mix", 10, 15, stageStart, stageStart.AddMinutes(10), stageStart.AddMinutes(15),
                        null, null, StageStatus.Pending, 0, false, null, null),
                ]),
            ],
        };
        if (save)
        {
            Batches[id] = (ownerId, dto);
        }

        return Task.FromResult(dto);
    }

    public Task<BatchDto?> ReplanAsync(Guid ownerId, Guid id, NewBatch batch, CancellationToken cancellationToken)
    {
        LastPlanned = batch;
        Calls.Add("replan");
        return GetAsync(ownerId, id, cancellationToken);
    }

    public Task<BatchStatus?> GetStatusAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.TryGetValue(id, out var b) && b.Owner == ownerId ? b.Batch.Status : (BatchStatus?)null);

    public Task SetStatusAsync(Guid ownerId, Guid id, BatchStatus status, CancellationToken cancellationToken)
    {
        Batches[id] = (ownerId, Batches[id].Batch with { Status = status });
        Calls.Add($"status:{status}");
        return Task.CompletedTask;
    }

    public Task<Guid?> GetBakeRecordIdAsync(Guid batchId, CancellationToken cancellationToken) => Task.FromResult(BakeRecordId);

    public Task RefreshStatisticsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("refresh");
        return Task.CompletedTask;
    }

    public Task<StageState?> GetStageAsync(Guid ownerId, Guid batchId, Guid stageId, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.TryGetValue(batchId, out var b) && b.Owner == ownerId && Stages.TryGetValue(stageId, out var s) ? s : null);

    public Task StartStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken) => Record("start");

    public Task CompleteStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken) => Record("complete");

    public Task SkipStageAsync(Guid stageId, StageActionRequest action, CancellationToken cancellationToken) => Record("skip");

    public Task ExtendStageAsync(Guid stageId, int minutes, CancellationToken cancellationToken) => Record($"extend:{minutes}");

    public Task ChangeStageDurationsAsync(Guid stageId, int minMinutes, int maxMinutes, bool overridden, CancellationToken cancellationToken) =>
        Record($"durations:{minMinutes}-{maxMinutes}:{overridden}");

    public Task<(int Min, int Max)?> GetRecipeStageDurationsAsync(Guid recipeStageId, CancellationToken cancellationToken) =>
        Task.FromResult(RecipeStageDurations);

    public Task<IReadOnlyList<KneadingItem>?> GetKneadingItemsAsync(Guid ownerId, Guid batchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KneadingItem>?>(Batches.TryGetValue(batchId, out var b) && b.Owner == ownerId ? KneadingItems : null);

    private Task Record(string call)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }

    private static BatchDto Batch(Guid id, BatchStatus status) => new(
        id, "Weekend", status, BatchSource.Manual, new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero), null, null, null, null,
        new DateOnly(2026, 11, 15), 11.22m, "EUR", null, [], null);
}

internal sealed class FakeStarterRepository : IStarterRepository
{
    public Dictionary<Guid, (Guid Owner, StarterDto Starter)> Starters { get; } = [];

    public List<FeedingDto> Feedings { get; } = [];

    public Guid Add(Guid owner, string name = "Rye starter")
    {
        var id = Guid.NewGuid();
        Starters[id] = (owner, ToDto(id, new StarterRequest(name, 100, FeedingMode.Regular, 24)));
        return id;
    }

    public Task<PagedResult<StarterDto>> ListAsync(Guid ownerId, PageQuery query, CancellationToken cancellationToken)
    {
        var items = Starters.Values.Where(s => s.Owner == ownerId).Select(s => s.Starter).ToList();
        return Task.FromResult(new PagedResult<StarterDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<StarterDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Starters.TryGetValue(id, out var s) && s.Owner == ownerId ? s.Starter : null);

    public Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Starters.TryGetValue(id, out var s) && s.Owner == ownerId);

    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Starters.Values.Any(s => s.Owner == ownerId && s.Starter.Name == name && s.Starter.Id != exceptId));

    public Task<Guid> CreateAsync(Guid ownerId, StarterRequest starter, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        Starters[id] = (ownerId, ToDto(id, starter));
        return Task.FromResult(id);
    }

    public Task<bool> UpdateAsync(Guid ownerId, Guid id, StarterRequest starter, CancellationToken cancellationToken)
    {
        if (!Starters.TryGetValue(id, out var s) || s.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Starters[id] = (ownerId, ToDto(id, starter));
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Starters.TryGetValue(id, out var s) && s.Owner == ownerId && Starters.Remove(id));

    public Task<PagedResult<FeedingDto>?> GetFeedingsAsync(Guid ownerId, Guid starterId, PageQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Starters.TryGetValue(starterId, out var s) && s.Owner == ownerId
            ? new PagedResult<FeedingDto>(Feedings, query.Page, query.PageSize, Feedings.Count)
            : null);

    public Task<FeedingDto> AddFeedingAsync(Guid starterId, FeedingRequest feeding, CancellationToken cancellationToken)
    {
        var dto = new FeedingDto(Guid.NewGuid(), feeding.FedAt, feeding.FlourG, feeding.WaterG, feeding.StarterKeptG,
            feeding.TemperatureC, feeding.FedAt.AddHours(24), feeding.FedAt.AddHours(12), feeding.Note);
        Feedings.Add(dto);
        return Task.FromResult(dto);
    }

    private static StarterDto ToDto(Guid id, StarterRequest r) => new(
        id, r.Name, r.FlourId, r.HydrationPercent, r.FeedingMode, r.FeedingIntervalHours, r.RiseHours, r.IsActive, r.Notes,
        null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}

internal sealed class FakeBakeRecordRepository : IBakeRecordRepository
{
    public Dictionary<Guid, (Guid Owner, BakeRecordDto Record)> Records { get; } = [];

    public Guid Add(Guid owner)
    {
        var id = Guid.NewGuid();
        Records[id] = (owner, new BakeRecordDto(id, Guid.NewGuid(), new DateOnly(2026, 11, 21), 3, 11.22m, "EUR", null, null, null,
            null, [new(Guid.NewGuid(), Guid.NewGuid(), "Loaf", 3, 11.22m)], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, []));
        return id;
    }

    public Task<PagedResult<BakeRecordSummaryDto>> ListAsync(Guid ownerId, BakeRecordListQuery query, CancellationToken cancellationToken)
    {
        var items = Records.Values.Where(r => r.Owner == ownerId)
            .Select(r => new BakeRecordSummaryDto(r.Record.Id, r.Record.BatchId, r.Record.BakedOn, r.Record.TotalLoaves, r.Record.TotalCost,
                r.Record.Currency, r.Record.Rating, r.Record.AssistantAnalysis != null, ["Loaf"])).ToList();
        return Task.FromResult(new PagedResult<BakeRecordSummaryDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<BakeRecordDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Records.TryGetValue(id, out var r) && r.Owner == ownerId ? r.Record : null);

    public Task<bool> UpdateFeedbackAsync(Guid ownerId, Guid id, short? rating, string? comment, CancellationToken cancellationToken)
    {
        if (!Records.TryGetValue(id, out var r) || r.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Records[id] = (ownerId, r.Record with { Rating = rating, Comment = comment });
        return Task.FromResult(true);
    }

    public Task<bool> SaveAnalysisAsync(Guid ownerId, Guid id, string analysis, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!Records.TryGetValue(id, out var r) || r.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Records[id] = (ownerId, r.Record with { AssistantAnalysis = analysis, AnalysisAt = at });
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<StageLogEntryDto>?> GetStageLogAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StageLogEntryDto>?>(Records.TryGetValue(id, out var r) && r.Owner == ownerId ? [] : null);

    public Task<IReadOnlyList<BakeStatisticsDto>> GetStatisticsAsync(Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BakeStatisticsDto>>(
            [new(Guid.NewGuid(), "Loaf", 1, 3, null, 0, 3.74m, new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 21))]);
}
