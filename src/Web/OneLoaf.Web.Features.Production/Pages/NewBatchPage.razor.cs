using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using OneLoaf.Web.Features.Production.Models;
using OneLoaf.Web.Features.Production.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Forms;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Production.Pages;

/// <summary>
/// <c>/batches/new</c> (US-20, US-29, US-52): name, start or ready-by time, recipes with loaves, starter and reminder;
/// the preview (cost, ready window, starter, kneading) comes from <c>POST /batches/preview</c> after every change.
/// </summary>
public sealed partial class NewBatchPage
{
    private const string InvalidText = "Check the highlighted fields; the batch is not saved yet.";

    private static readonly IReadOnlyList<SegmentOption<PlanMode>> ModeOptions =
        [new(PlanMode.StartAt, "Start time"), new(PlanMode.ReadyBy, "Ready by")];

    private static readonly IReadOnlyList<SegmentOption<int>> ReminderOptions =
        [new(60, "60 min"), new(30, "30 min")];

    private readonly BatchForm _form = new();
    private EditContext? _editContext;
    private ServerValidation? _server;
    private string? _summary;
    private string? _summaryReference;

    [Inject]
    private IState<NewBatchState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Recipe to start with (from "Plan a batch" on a recipe).</summary>
    [SupplyParameterFromQuery(Name = "recipeId")]
    public Guid? RecipeId { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        var start = Time.GetLocalNow().AddHours(1);
        _form.Date = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _form.Time = start.ToString("HH:00", CultureInfo.InvariantCulture);
        _form.Items.Add(new BatchItemForm { RecipeId = RecipeId?.ToString() });
        _editContext = new EditContext(_form);
        _editContext.OnFieldChanged += (_, _) => SchedulePreview();
        SubscribeToAction<NewBatchDataLoaded>(_ => SchedulePreview());
        SubscribeToAction<BatchCreateFailed>(a => ShowError(a.Error));
        Dispatcher.Dispatch(new OpenNewBatch());
    }

    private static IReadOnlyList<SelectOption> RecipeOptions(NewBatchData data) =>
        [.. data.Recipes.Where(r => !r.IsArchived).Select(r => new SelectOption(r.Id.ToString(), r.Title))];

    private static IReadOnlyList<SelectOption> StarterOptions(NewBatchData data) =>
        [.. data.Starters.Where(s => s.IsActive).Select(s => new SelectOption(s.Id.ToString(), s.Name))];

    private static string RecipeTitle(NewBatchData data, BatchItemForm item) =>
        data.Recipes.FirstOrDefault(r => r.Id.ToString() == item.RecipeId)?.Title ?? "recipe";

    private void SetMode(PlanMode mode)
    {
        _form.Mode = mode;
        SchedulePreview();
    }

    private void SetLoaves(BatchItemForm item, int loaves)
    {
        item.Loaves = loaves;
        SchedulePreview();
    }

    private void SetReminder(BatchItemForm item, int minutes)
    {
        item.ReminderMinutes = minutes;
        SchedulePreview();
    }

    private void AddItem()
    {
        _form.Items.Add(new BatchItemForm());
        SchedulePreview();
    }

    private void RemoveItem(int index)
    {
        _form.Items.RemoveAt(index);
        SchedulePreview();
    }

    /// <summary>Asks for a preview when the form is complete enough; otherwise clears it.</summary>
    private void SchedulePreview()
    {
        if (!_form.CanPreview || _form.Moment() is not { } moment)
        {
            Dispatcher.Dispatch(new ClearPreview());
            return;
        }

        var readyAt = _form.Mode == PlanMode.ReadyBy ? moment : (DateTimeOffset?)null;
        Dispatcher.Dispatch(new PreviewBatch(Guid.NewGuid(), _form.ToRequest(moment), readyAt));
    }

    private void Save()
    {
        if (State.Value.Saving || _form.Moment() is not { } moment)
        {
            return;
        }

        _summary = null;
        var start = moment;
        if (_form.Mode == PlanMode.ReadyBy)
        {
            if (State.Value.Preview is not Loadable<BatchPreview>.Loaded { Data: var preview })
            {
                _summary = "Wait for the preview: the start time is planned from the ready-by time.";
                return;
            }

            start = preview.Batch.PlannedStartAt;
        }

        Dispatcher.Dispatch(new CreateBatch(_form.ToRequest(start)));
    }

    private void ShowError(ApiError error)
    {
        _summaryReference = error.CorrelationId;
        _summary = error.Kind == ApiErrorKind.Validation && _server is not null
            ? _server.Display(error.FieldErrors) is { Count: > 0 } unmatched ? string.Join(" ", unmatched) : InvalidText
            : error.Message;
        StateHasChanged();
    }
}
