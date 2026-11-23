using Fluxor;
using Fluxor.Blazor.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using OneLoaf.Web.Features.History.Models;
using OneLoaf.Web.Features.History.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Forms;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.History.Pages;

/// <summary>Bake record (US-32, US-36, US-51): feedback, assistant analysis, stage log, crumb photos.</summary>
public partial class BakeRecordPage : FluxorComponent
{
    /// <summary>Largest photo the Core API accepts.</summary>
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    private FeedbackForm? _form;
    private ServerValidation? _server;
    private OlTextArea? _comment;
    private ApiError? _feedbackError;
    private ApiError? _analysisError;
    private ApiError? _photoError;
    private bool _focusComment;

    /// <summary>Bake record id.</summary>
    [Parameter] public Guid Id { get; set; }

    [Inject] private IState<BakeRecordState> State { get; set; } = default!;

    [Inject] private IDispatcher Dispatcher { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToAction<BakeRecordLoaded>(a =>
        {
            if (a.Id == Id && a.Record is Loadable<BakeRecordDto>.Loaded { Data: var record })
            {
                _form ??= FeedbackForm.From(record);
            }
        });
        SubscribeToAction<BakeRated>(a => _form = FeedbackForm.From(a.Record));
        SubscribeToAction<BakeRecordCommandFailed>(OnFailed);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (State.Value.RecordId != Id || State.Value.Record is Loadable<BakeRecordDto>.NotAsked or Loadable<BakeRecordDto>.Failed)
        {
            _form = null;
            _feedbackError = _analysisError = _photoError = null;
            Dispatcher.Dispatch(new OpenBakeRecord(Id));
        }
        else if (State.Value.Record.Value is { } record)
        {
            _form ??= FeedbackForm.From(record);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_focusComment && _comment is not null)
        {
            _focusComment = false;
            await _comment.FocusAsync();
        }
    }

    private void OnFailed(BakeRecordCommandFailed failed)
    {
        switch (failed.Command)
        {
            case nameof(RateBake):
                _feedbackError = failed.Error.Kind == ApiErrorKind.Validation && _server is not null && _server.Display(failed.Error.FieldErrors).Count == 0
                    ? null
                    : failed.Error;
                break;
            case nameof(AnalyseBake):
                _analysisError = failed.Error;
                _focusComment = failed.Error.Kind == ApiErrorKind.BusinessRule;
                break;
            default:
                _photoError = failed.Error;
                break;
        }
    }

    private void SaveFeedback()
    {
        if (State.Value.Saving || _form is null)
        {
            return;
        }

        _feedbackError = null;
        Dispatcher.Dispatch(new RateBake(Id, _form.ToRequest()));
    }

    private void Analyse()
    {
        if (State.Value.Analysing)
        {
            return;
        }

        _analysisError = null;
        Dispatcher.Dispatch(new AnalyseBake(Id));
    }

    private async Task Upload(InputFileChangeEventArgs e)
    {
        _photoError = null;
        var file = e.File;
        if (file.Size > MaxPhotoBytes)
        {
            _photoError = ApiError.FromStatus(422) with { Detail = "The photo is larger than 5 MB." };
            return;
        }

        using var buffer = new MemoryStream();
        await using (var stream = file.OpenReadStream(MaxPhotoBytes))
        {
            await stream.CopyToAsync(buffer);
        }

        Dispatcher.Dispatch(new UploadPhoto(Id, buffer.ToArray(), file.Name, file.ContentType));
    }

    private static string AnalysisMessage(ApiError error) => error switch
    {
        { Kind: ApiErrorKind.RateLimited } => "You have reached the assistant limit for now. Try again later.",
        _ => error.Message,
    };

    private static string StageName(StageLogEntryDto stage, IReadOnlyList<StageLogEntryDto> log) =>
        log.Select(s => s.BatchItemId).Distinct().Skip(1).Any() ? $"{stage.RecipeTitle} · {stage.StageName}" : stage.StageName;
}
