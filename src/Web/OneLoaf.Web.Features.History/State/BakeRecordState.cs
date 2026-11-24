using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.History.State;

/// <summary>Bake record slice (US-32, US-36, US-51): record, stage log, photos as data URLs, analysis.</summary>
[FeatureState(Name = "BakeRecord")]
public sealed record BakeRecordState(
    Guid? RecordId,
    Loadable<BakeRecordDto> Record,
    Loadable<IReadOnlyList<StageLogEntryDto>> StageLog,
    ImmutableDictionary<Guid, string> PhotoUrls,
    bool Saving,
    bool Analysing,
    bool Uploading,
    IReadOnlyList<string> UnverifiedNumbers)
{
    private BakeRecordState()
        : this(null, Loadable<BakeRecordDto>.Idle, Loadable<IReadOnlyList<StageLogEntryDto>>.Idle, ImmutableDictionary<Guid, string>.Empty,
            false, false, false, [])
    {
    }
}

/// <summary>Intent: open a bake record.</summary>
public sealed record OpenBakeRecord(Guid Id);

/// <summary>Result: record and stage log.</summary>
public sealed record BakeRecordLoaded(Guid Id, Loadable<BakeRecordDto> Record, Loadable<IReadOnlyList<StageLogEntryDto>> StageLog);

/// <summary>Result: a photo's bytes arrived.</summary>
public sealed record PhotoLoaded(Guid RecordId, Guid PhotoId, string DataUrl);

/// <summary>Intent: save rating and comment (US-32).</summary>
public sealed record RateBake(Guid Id, UpdateBakeRecordRequest Request);

/// <summary>Result: saved; the record from the API replaces the shown one.</summary>
public sealed record BakeRated(BakeRecordDto Record);

/// <summary>Intent: ask the assistant to analyse the bake (US-36).</summary>
public sealed record AnalyseBake(Guid Id);

/// <summary>Result: analysis stored and returned.</summary>
public sealed record BakeAnalysed(Guid Id, AnalysisDto Analysis);

/// <summary>Intent: upload a crumb photo (US-51).</summary>
public sealed record UploadPhoto(Guid Id, byte[] Content, string FileName, string ContentType);

/// <summary>Intent: delete a photo.</summary>
public sealed record DeletePhoto(Guid Id, Guid PhotoId);

/// <summary>Result: a record command failed; <paramref name="Command"/> tells the page where to show it.</summary>
public sealed record BakeRecordCommandFailed(string Command, ApiError Error);

/// <summary>Reducers of <see cref="BakeRecordState"/>.</summary>
public static class BakeRecordReducers
{
    /// <summary>Opening.</summary>
    [ReducerMethod]
    public static BakeRecordState OnOpen(BakeRecordState state, OpenBakeRecord action) =>
        state.RecordId == action.Id
            ? state with { Record = state.Record.Reload() }
            : new BakeRecordState(action.Id, new Loadable<BakeRecordDto>.Loading(null), Loadable<IReadOnlyList<StageLogEntryDto>>.Idle,
                ImmutableDictionary<Guid, string>.Empty, false, false, false, []);

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static BakeRecordState OnLoaded(BakeRecordState state, BakeRecordLoaded action) =>
        action.Id == state.RecordId ? state with { Record = action.Record, StageLog = action.StageLog } : state;

    /// <summary>Photo bytes.</summary>
    [ReducerMethod]
    public static BakeRecordState OnPhoto(BakeRecordState state, PhotoLoaded action) =>
        action.RecordId == state.RecordId ? state with { PhotoUrls = state.PhotoUrls.SetItem(action.PhotoId, action.DataUrl) } : state;

    /// <summary>Saving.</summary>
    [ReducerMethod(typeof(RateBake))]
    public static BakeRecordState OnRate(BakeRecordState state) => state with { Saving = true };

    /// <summary>Saved.</summary>
    [ReducerMethod]
    public static BakeRecordState OnRated(BakeRecordState state, BakeRated action) =>
        action.Record.Id == state.RecordId ? state with { Saving = false, Record = new Loadable<BakeRecordDto>.Loaded(action.Record) } : state;

    /// <summary>Analysing (can take several seconds).</summary>
    [ReducerMethod(typeof(AnalyseBake))]
    public static BakeRecordState OnAnalyse(BakeRecordState state) => state with { Analysing = true, UnverifiedNumbers = [] };

    /// <summary>Analysis arrived; it is stored in the record by the assistant.</summary>
    [ReducerMethod]
    public static BakeRecordState OnAnalysed(BakeRecordState state, BakeAnalysed action) =>
        action.Id == state.RecordId && state.Record.Value is { } record
            ? state with
            {
                Analysing = false,
                UnverifiedNumbers = action.Analysis.UnverifiedNumbers,
                Record = new Loadable<BakeRecordDto>.Loaded(record with { AssistantAnalysis = action.Analysis.Analysis }),
            }
            : state with { Analysing = false };

    /// <summary>Uploading.</summary>
    [ReducerMethod(typeof(UploadPhoto))]
    public static BakeRecordState OnUpload(BakeRecordState state) => state with { Uploading = true };

    /// <summary>A command failed.</summary>
    [ReducerMethod(typeof(BakeRecordCommandFailed))]
    public static BakeRecordState OnFailed(BakeRecordState state) => state with { Saving = false, Analysing = false, Uploading = false };

    /// <summary>The list shows the new rating after a reload.</summary>
    [ReducerMethod(typeof(BakeRated))]
    public static HistoryState ListAfterRating(HistoryState state) =>
        state with { List = Loadable<HistoryList>.Idle, Statistics = Loadable<IReadOnlyList<BakeStatisticsDto>>.Idle };
}

/// <summary>Side effects of the bake record.</summary>
public sealed class BakeRecordEffects(IHistoryApi api)
{
    /// <summary>Record and stage log in parallel, then the photos one by one.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenBakeRecord action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var record = api.GetAsync(action.Id);
        var log = api.GetStageLogAsync(action.Id);
        var recordResult = (await record).ToLoadable();
        dispatcher.Dispatch(new BakeRecordLoaded(action.Id, recordResult, (await log).ToLoadable()));
        if (recordResult is Loadable<BakeRecordDto>.Loaded { Data: var loaded })
        {
            await LoadPhotos(loaded, dispatcher);
        }
    }

    /// <summary>Saves rating and comment.</summary>
    [EffectMethod]
    public async Task HandleRate(RateBake action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.RateAsync(action.Id, action.Request))
        {
            case ApiResult<BakeRecordDto>.Success { Value: var record }:
                dispatcher.Dispatch(new BakeRated(record));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Feedback saved")));
                break;
            case ApiResult<BakeRecordDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new BakeRecordCommandFailed(nameof(RateBake), error));
                break;
        }
    }

    /// <summary>Asks the assistant; 422 means rating or comment are missing, 429/503 the assistant is limited or down.</summary>
    [EffectMethod]
    public async Task HandleAnalyse(AnalyseBake action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AnalyseAsync(action.Id))
        {
            case ApiResult<AnalysisDto>.Success { Value: var analysis }:
                dispatcher.Dispatch(new BakeAnalysed(action.Id, analysis));
                break;
            case ApiResult<AnalysisDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new BakeRecordCommandFailed(nameof(AnalyseBake), error));
                break;
        }
    }

    /// <summary>Uploads a photo and reloads the record.</summary>
    [EffectMethod]
    public async Task HandleUpload(UploadPhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        using var stream = new MemoryStream(action.Content);
        switch (await api.UploadPhotoAsync(action.Id, stream, action.FileName, action.ContentType))
        {
            case ApiResult<BakeRecordPhotoDto>.Success:
                dispatcher.Dispatch(new ShowToast(Toast.Success("Photo added")));
                dispatcher.Dispatch(new OpenBakeRecord(action.Id));
                break;
            case ApiResult<BakeRecordPhotoDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new BakeRecordCommandFailed(nameof(UploadPhoto), error));
                break;
        }
    }

    /// <summary>Deletes a photo and reloads the record.</summary>
    [EffectMethod]
    public async Task HandleDeletePhoto(DeletePhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeletePhotoAsync(action.Id, action.PhotoId))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new BakeRecordCommandFailed(nameof(DeletePhoto), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new OpenBakeRecord(action.Id));
                break;
        }
    }

    private async Task LoadPhotos(BakeRecordDto record, IDispatcher dispatcher)
    {
        foreach (var photo in record.Photos)
        {
            if (await api.GetPhotoAsync(photo.Url) is ApiResult<FileDownload>.Success { Value: var file })
            {
                dispatcher.Dispatch(new PhotoLoaded(record.Id, photo.Id, file.ToDataUrl()));
            }
        }
    }
}
