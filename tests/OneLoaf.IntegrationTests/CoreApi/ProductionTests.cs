using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;
using OneLoaf.Messaging;
using RabbitMQ.Client;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Production planning, stage execution, starters and history end to end (US-20 - US-33, US-36).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class ProductionTests(ApiFixture fixture)
{
    private const string Batches = "/api/v1/batches";

    private static readonly DateTimeOffset Start = new(2030, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static BatchRequest AnnaBatch(string name, int loaves = 3) =>
        new(name, Start, [new(Seed.R1, loaves, null, false)], new DateOnly(2026, 11, 15));

    [Fact]
    public async Task US20_CreateBatch_PlanFromTheDatabase()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        using var response = await anna.PostAsJsonAsync(Batches, AnnaBatch("Plan check"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var batch = await response.ReadAsync<BatchDto>();
        Assert.Equal(BatchStatus.Planned, batch.Status);
        Assert.Equal(11.22m, batch.TotalCost);
        var item = Assert.Single(batch.Items);
        Assert.Equal(5, item.Stages.Count);
        Assert.Equal(300m, item.StarterAmountG);
        Assert.Equal(1, item.KneadingCycles);
        Assert.Equal(Start.AddHours(14), batch.PlannedReadyFrom);
        Assert.Equal(Start.AddHours(16), batch.PlannedReadyTo);
        Assert.Equal($"{Batches}/{batch.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task US34_Preview_ReturnsThePlanWithoutSaving()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var preview = await (await anna.PostAsJsonAsync($"{Batches}/preview", AnnaBatch("Preview only"), CoreApiFactory.Json, Ct))
            .ReadAsync<BatchDto>();
        var calendar = await (await anna.GetAsync($"{Batches}?pageSize=100", Ct)).ReadAsync<PagedResult<BatchSummaryDto>>();

        Assert.Equal(Guid.Empty, preview.Id);
        Assert.Equal(11.22m, preview.TotalCost);
        Assert.Equal(5, preview.Items[0].Stages.Count);
        Assert.DoesNotContain(calendar.Items, b => b.Name == "Preview only");
    }

    [Fact]
    public async Task US23_US24_US21_US31_US32_FullBatchLifecycle()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var batch = await (await anna.PostAsJsonAsync(Batches, AnnaBatch("Lifecycle", 2), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        var stages = batch.Items[0].Stages;
        string Stage(int index) => $"{Batches}/{batch.Id}/stages/{stages[index].Id}";

        // US-21: change the second stage for this batch only, then reset it to the recipe values.
        var changed = await (await anna.PatchAsJsonAsync(Stage(1), new StageDurationRequest(stages[1].PlannedMinMinutes + 30,
            stages[1].PlannedMaxMinutes + 30), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        Assert.True(changed.Items[0].Stages[1].IsOverridden);
        Assert.Equal(batch.PlannedReadyTo!.Value.AddMinutes(30), changed.PlannedReadyTo);
        var reset = await (await anna.PostAsync($"{Stage(1)}/reset", null, Ct)).ReadAsync<BatchDto>();
        Assert.False(reset.Items[0].Stages[1].IsOverridden);
        Assert.Equal(batch.PlannedReadyTo, reset.PlannedReadyTo);

        // US-23: starting the first stage moves the batch to in_progress.
        var started = await (await anna.PostAsJsonAsync($"{Stage(0)}/start", new StageActionRequest(26m, null), CoreApiFactory.Json, Ct))
            .ReadAsync<BatchDto>();
        Assert.Equal(BatchStatus.InProgress, started.Status);
        Assert.Equal(StageStatus.Running, started.Items[0].Stages[0].Status);

        using var startAgain = await anna.PostAsJsonAsync($"{Stage(0)}/start", new StageActionRequest(), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, startAgain.StatusCode);

        // US-24: "+30 min" shifts the ready window.
        var extended = await (await anna.PostAsJsonAsync($"{Stage(0)}/extend", new ExtendStageRequest(30), CoreApiFactory.Json, Ct))
            .ReadAsync<BatchDto>();
        Assert.Equal(30, extended.Items[0].Stages[0].ExtendedMinutes);
        Assert.Equal(batch.PlannedReadyTo!.Value.AddMinutes(30), extended.PlannedReadyTo);

        var completedStage = await (await anna.PostAsJsonAsync($"{Stage(0)}/complete", new StageActionRequest(null, "ok"), CoreApiFactory.Json, Ct))
            .ReadAsync<BatchDto>();
        Assert.Equal(StageStatus.Done, completedStage.Items[0].Stages[0].Status);

        using var replanStarted = await anna.PutAsJsonAsync($"{Batches}/{batch.Id}", AnnaBatch("Too late"), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, replanStarted.StatusCode);

        // US-31: completing creates the history record; batch.completed is published.
        using var broker = await fixture.ConnectToBrokerAsync();
        await using var channel = await broker.CreateChannelAsync(cancellationToken: Ct);
        await channel.ExchangeDeclareAsync(Exchanges.Events, ExchangeType.Topic, durable: true, cancellationToken: Ct);
        var queue = (await channel.QueueDeclareAsync(cancellationToken: Ct)).QueueName;
        await channel.QueueBindAsync(queue, Exchanges.Events, RoutingKeys.BatchCompleted, cancellationToken: Ct);

        var completion = await (await anna.PostAsync($"{Batches}/{batch.Id}/complete", null, Ct)).ReadAsync<CompleteBatchResponse>();
        Assert.NotNull(completion.BakeRecordId);

        var message = await WaitForMessageAsync(channel, queue);
        using (var json = JsonDocument.Parse(message))
        {
            Assert.Equal(batch.Id, json.RootElement.GetProperty("batchId").GetGuid());
        }

        using var completeAgain = await anna.PostAsync($"{Batches}/{batch.Id}/complete", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, completeAgain.StatusCode);

        // US-32: rating and comment; US-36: stage log; statistics.
        var recordUrl = $"/api/v1/bake-records/{completion.BakeRecordId}";
        var rated = await (await anna.PatchAsJsonAsync(recordUrl, new UpdateBakeRecordRequest(4, "Open crumb"), CoreApiFactory.Json, Ct))
            .ReadAsync<BakeRecordDto>();
        Assert.Equal((short)4, rated.Rating);
        Assert.Equal(2, rated.TotalLoaves);
        var log = await (await anna.GetAsync($"{recordUrl}/stage-log", Ct)).ReadAsync<List<StageLogEntryDto>>();
        Assert.Equal(5, log.Count);
        Assert.Equal(StageStatus.Done, log[0].StageStatus);
        var analysed = await (await anna.PutAsJsonAsync($"{recordUrl}/analysis", new SaveAnalysisRequest("Bulk ran long."), CoreApiFactory.Json, Ct))
            .ReadAsync<BakeRecordDto>();
        Assert.Equal("Bulk ran long.", analysed.AssistantAnalysis);
        var statistics = await (await anna.GetAsync("/api/v1/bake-records/statistics", Ct)).ReadAsync<List<BakeStatisticsDto>>();
        Assert.Contains(statistics, s => s.RecipeId == Seed.R1);
        var history = await (await anna.GetAsync($"/api/v1/bake-records?recipeId={Seed.R1}&minRating=4", Ct))
            .ReadAsync<PagedResult<BakeRecordSummaryDto>>();
        Assert.Contains(history.Items, r => r.Id == completion.BakeRecordId);
    }

    [Fact]
    public async Task Cancel_PlannedBatch_ThenNotAllowedAgain()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var batch = await (await anna.PostAsJsonAsync(Batches, AnnaBatch("To cancel", 1), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();

        using var cancel = await anna.PostAsync($"{Batches}/{batch.Id}/cancel", null, Ct);
        using var again = await anna.PostAsync($"{Batches}/{batch.Id}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task US22_KneadingPlan_MergesItemsOfTheSameDough()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var request = new BatchRequest("Two of the same", Start, [new(Seed.R1, 3), new(Seed.R1, 2)], new DateOnly(2026, 11, 15));
        var batch = await (await anna.PostAsJsonAsync(Batches, request, CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();

        var plan = await (await anna.GetAsync($"{Batches}/{batch.Id}/kneading-plan", Ct)).ReadAsync<KneadingPlanDto>();

        var group = Assert.Single(plan.Groups);
        Assert.Equal(5, group.Loaves);
        Assert.Equal(4800m, group.DoughWeightG);
        Assert.Equal(2, group.SeparateCycles);
        Assert.Equal(2, group.MergedCycles);
    }

    [Fact]
    public async Task Batch_InvalidInputAndForeignRecipe_Return400()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        using var empty = await boris.PostAsJsonAsync(Batches, new BatchRequest("Empty", Start, []), CoreApiFactory.Json, Ct);
        using var foreign = await boris.PostAsJsonAsync(Batches, AnnaBatch("Stolen"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task US03_OtherUsersBatchAndRecord_Return404()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        var batch = await (await anna.PostAsJsonAsync(Batches, AnnaBatch("Private", 1), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        var stage = batch.Items[0].Stages[0].Id;

        foreach (var (method, url) in new[]
        {
            ("GET", $"{Batches}/{batch.Id}"),
            ("POST", $"{Batches}/{batch.Id}/cancel"),
            ("POST", $"{Batches}/{batch.Id}/complete"),
            ("GET", $"{Batches}/{batch.Id}/kneading-plan"),
            ("POST", $"{Batches}/{batch.Id}/stages/{stage}/reset"),
        })
        {
            using var response = await boris.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var start = await boris.PostAsJsonAsync($"{Batches}/{batch.Id}/stages/{stage}/start", new StageActionRequest(), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        var calendar = await (await boris.GetAsync($"{Batches}?pageSize=100", Ct)).ReadAsync<PagedResult<BatchSummaryDto>>();
        Assert.DoesNotContain(calendar.Items, b => b.Id == batch.Id);
    }

    [Fact]
    public async Task US27_US28_US30_StarterAndFeedingSchedule()
    {
        using var client = await fixture.Api.CreateAuthenticatedClientAsync(await fixture.Api.RegisterAsync());

        using var created = await client.PostAsJsonAsync("/api/v1/starters",
            new StarterRequest("Rye mother", 100, FeedingMode.Regular, 24, RiseHours: 12), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var starter = await created.ReadAsync<StarterDto>();

        var fedAt = new DateTimeOffset(2030, 1, 10, 8, 0, 0, TimeSpan.Zero);
        using var fed = await client.PostAsJsonAsync($"/api/v1/starters/{starter.Id}/feedings",
            new FeedingRequest(fedAt, 50, 50, 20, 24), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, fed.StatusCode);
        var feeding = await fed.ReadAsync<FeedingDto>();

        Assert.Equal(fedAt.AddHours(24), feeding.NextFeedingAt);
        Assert.Equal(fedAt.AddHours(12), feeding.ExpectedPeakAt);
        var reloaded = await (await client.GetAsync($"/api/v1/starters/{starter.Id}", Ct)).ReadAsync<StarterDto>();
        Assert.Equal(fedAt, reloaded.LastFedAt);
        var log = await (await client.GetAsync($"/api/v1/starters/{starter.Id}/feedings", Ct)).ReadAsync<PagedResult<FeedingDto>>();
        Assert.Single(log.Items);

        using var duplicate = await client.PostAsJsonAsync("/api/v1/starters",
            new StarterRequest("Rye mother", 100, FeedingMode.Regular, 24, RiseHours: 12), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var delete = await client.DeleteAsync($"/api/v1/starters/{starter.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    private static async Task<string> WaitForMessageAsync(IChannel channel, string queue)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true, Ct);
            if (result is not null)
            {
                return Encoding.UTF8.GetString(result.Body.Span);
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException("No message was published.");
    }
}
