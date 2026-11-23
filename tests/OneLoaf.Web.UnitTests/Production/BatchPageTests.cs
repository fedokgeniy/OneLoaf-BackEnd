using OneLoaf.Web.Features.Production.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Production;

public sealed class BatchPageTests : BunitContext
{
    private static readonly Guid BatchId = Guid.NewGuid();
    private readonly Api _api = new();

    public BatchPageTests() => this.AddProductionFakes(_api);

    [Fact]
    public void Start_CurrentStage_CallsTheApiAndShowsTheReturnedBatch()
    {
        var cut = Render<BatchPage>(p => p.Add(page => page.Id, BatchId));
        cut.WaitForAssertion(() => Assert.Contains("Stage 1 of 2", cut.Find(".batch__summary").TextContent));

        cut.Find(".batch__current .ol-stage__actions .ol-btn--primary").Click();

        cut.WaitForAssertion(() => Assert.Equal("Running", cut.Find(".batch__current .ol-badge").TextContent));
        Assert.Equal(("start", _api.Stages[0].Id), _api.LastAction);
        Assert.Equal("0", cut.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Start_StageAlreadyRunningElsewhere_ShowsErrorAndReloads()
    {
        _api.Conflict = true;
        var cut = Render<BatchPage>(p => p.Add(page => page.Id, BatchId));
        cut.WaitForAssertion(() => cut.Find(".batch__current .ol-stage__actions .ol-btn--primary"));
        var loadsBefore = _api.Loads;

        cut.Find(".batch__current .ol-stage__actions .ol-btn--primary").Click();

        cut.WaitForAssertion(() => Assert.True(_api.Loads > loadsBefore));
    }

    private sealed class Api : ProductionApiStub
    {
        private BatchDto _batch;

        public Api()
        {
            var start = DateTimeOffset.UnixEpoch;
            Stages =
            [
                new BatchStageDto(Guid.NewGuid(), 1, "Mix", "mix", 15, 20, start, start.AddMinutes(15), start.AddMinutes(20), null, null, StageStatuses.Pending, 0, false, null, null),
                new BatchStageDto(Guid.NewGuid(), 2, "Bake", "bake", 40, 45, start, start.AddMinutes(40), start.AddMinutes(45), null, null, StageStatuses.Pending, 0, false, null, null),
            ];
            _batch = new BatchDto(BatchId, "Saturday market", BatchStatuses.Planned, "manual", start, start.AddHours(1), start.AddHours(2), null, null,
                new DateOnly(2026, 11, 23), 8.94m, "EUR", null,
                [new BatchItemDto(Guid.NewGuid(), Guid.NewGuid(), "Country sourdough", 3, null, null, 1, null, 1, Stages)], null);
        }

        public List<BatchStageDto> Stages { get; }

        public bool Conflict { get; set; }

        public int Loads { get; private set; }

        public (string Action, Guid StageId)? LastAction { get; private set; }

        public override Task<ApiResult<BatchDto>> GetBatchAsync(Guid id, CancellationToken ct = default)
        {
            Loads++;
            return Ok(_batch);
        }

        public override Task<ApiResult<BatchDto>> StageActionAsync(Guid batchId, Guid stageId, string action, StageActionRequest request, CancellationToken ct = default)
        {
            LastAction = (action, stageId);
            if (Conflict)
            {
                return Task.FromResult(ApiResult<BatchDto>.Fail(ApiError.FromStatus(409)));
            }

            var running = Stages[0] with { Status = StageStatuses.Running, ActualStartAt = DateTimeOffset.UnixEpoch };
            _batch = _batch with
            {
                Status = BatchStatuses.InProgress,
                Items = [_batch.Items[0] with { Stages = [running, Stages[1]] }],
            };
            return Ok(_batch);
        }
    }
}
