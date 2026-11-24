using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Production.State;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Production.Pages;

/// <summary>
/// <c>/batches/{id}</c> (US-21–25, US-31): progress, the current stage with its actions, figures per recipe, the kneading
/// plan explanation, all stages, and the batch actions (complete, cancel, delete, save as variant). Cancel and
/// delete ask whether the ingredients were used, so the stock is right afterwards.
/// </summary>
public sealed partial class BatchPage
{
    private BatchStageDto? _adjust;
    private int _adjustMin;
    private int _adjustMax;
    private bool _confirmCancel;
    private bool _confirmDelete;
    private bool _ingredientsUsed;
    private string? _variantTitle;

    [Inject]
    private IState<BatchState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Batch id.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToAction<BatchDeleted>(deleted =>
        {
            if (deleted.Id == Id)
            {
                Navigation.NavigateTo("batches");
            }
        });
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (State.Value.BatchId != Id || State.Value.Batch is Shared.State.Loadable<BatchDto>.NotAsked)
        {
            Dispatcher.Dispatch(new OpenBatch(Id));
        }
    }

    /// <summary>Stages of all items in batch order.</summary>
    private static List<BatchStageDto> AllStages(BatchDto batch) =>
        [.. batch.Items.OrderBy(i => i.SortOrder).SelectMany(i => i.Stages.OrderBy(s => s.SortOrder))];

    /// <summary>The running stage, else the first pending one, else none.</summary>
    private static BatchStageDto? CurrentStage(List<BatchStageDto> stages) =>
        stages.FirstOrDefault(s => s.Status == StageStatuses.Running) ?? stages.FirstOrDefault(s => s.Status == StageStatuses.Pending);

    private static int Finished(List<BatchStageDto> stages) =>
        stages.Count(s => s.Status is StageStatuses.Done or StageStatuses.Skipped);

    private static string ProgressWidth(List<BatchStageDto> stages) =>
        stages.Count == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Finished(stages) * 100 / stages.Count}%");

    private static string StatusBadge(string status) => status switch
    {
        BatchStatuses.InProgress => "ol-badge--running",
        BatchStatuses.Done => "ol-badge--done",
        BatchStatuses.Cancelled => "ol-badge--neutral",
        _ => "ol-badge--planned",
    };

    private static string StatusText(string status) => status switch
    {
        BatchStatuses.InProgress => "In progress",
        BatchStatuses.Done => "Done",
        BatchStatuses.Cancelled => "Cancelled",
        _ => "Planned",
    };

    private void Run(BatchDto batch, BatchStageDto stage, StageCommandKind kind)
    {
        if (State.Value.BusyStageId is null)
        {
            Dispatcher.Dispatch(new RunStageCommand(batch.Id, stage.Id, kind));
        }
    }

    private void OpenAdjust(BatchStageDto stage)
    {
        _adjust = stage;
        _adjustMin = stage.PlannedMinMinutes;
        _adjustMax = stage.PlannedMaxMinutes;
    }

    private void SubmitAdjust(BatchDto batch, bool reset)
    {
        if (_adjust is not { } stage)
        {
            return;
        }

        Dispatcher.Dispatch(reset
            ? new RunStageCommand(batch.Id, stage.Id, StageCommandKind.Reset)
            : new RunStageCommand(batch.Id, stage.Id, StageCommandKind.Adjust, new StageDurationRequest(_adjustMin, _adjustMax)));
        _adjust = null;
    }

    private string StockHint => _ingredientsUsed
        ? "What the batch took from the stock stays taken."
        : "What the batch took from the stock is returned.";

    private void CancelBatch(BatchDto batch)
    {
        _confirmCancel = false;
        Dispatcher.Dispatch(new CancelBatch(batch.Id, _ingredientsUsed));
    }

    /// <summary>A batch that has started has usually used its ingredients; a planned one has not.</summary>
    private void AskCancel(BatchDto batch)
    {
        _ingredientsUsed = batch.Status == BatchStatuses.InProgress;
        _confirmCancel = true;
    }

    /// <summary>A planned or cancelled batch has usually not touched the shelf; a baked one has.</summary>
    private void AskDelete(BatchDto batch)
    {
        _ingredientsUsed = batch.Status == BatchStatuses.Done;
        _confirmDelete = true;
    }

    private void DeleteBatch(BatchDto batch)
    {
        _confirmDelete = false;
        Dispatcher.Dispatch(new DeleteBatch(batch.Id, _ingredientsUsed));
    }

    private void SaveVariant(BatchDto batch)
    {
        var title = string.IsNullOrWhiteSpace(_variantTitle) ? null : _variantTitle.Trim();
        _variantTitle = null;
        Dispatcher.Dispatch(new SaveBatchAsVariant(batch.Id, batch.Items.OrderBy(i => i.SortOrder).FirstOrDefault()?.Id, title));
    }
}
