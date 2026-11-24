using Fluxor;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Library.State;

/// <summary>Moderation queue for administrators (LR-11).</summary>
[FeatureState(Name = "Moderation")]
public sealed record ModerationState(string Status, Loadable<PagedList<ReportDto>> Reports, Guid? Resolving)
{
    /// <summary>Report statuses of the Core API.</summary>
    public static IReadOnlyList<string> Statuses { get; } = ["open", "resolved", "rejected"];

    private ModerationState()
        : this("open", Loadable<PagedList<ReportDto>>.Idle, null)
    {
    }
}

/// <summary>Intent: load the reports with a status.</summary>
public sealed record LoadReports(string Status);

/// <summary>Result: reports.</summary>
public sealed record ReportsLoaded(string Status, Loadable<PagedList<ReportDto>> Reports);

/// <summary>Intent: decide a report.</summary>
public sealed record ResolveReport(Guid ReportId, ResolveReportRequest Request);

/// <summary>Result: decided (or failed with <see cref="Error"/>).</summary>
public sealed record ReportResolved(Guid ReportId, ApiError? Error);

/// <summary>Reducers of <see cref="ModerationState"/>.</summary>
public static class ModerationReducers
{
    /// <summary>Loading.</summary>
    [ReducerMethod]
    public static ModerationState OnLoad(ModerationState state, LoadReports action) =>
        state with { Status = action.Status, Reports = state.Status == action.Status ? state.Reports.Reload() : new Loadable<PagedList<ReportDto>>.Loading(null) };

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static ModerationState OnLoaded(ModerationState state, ReportsLoaded action) =>
        action.Status == state.Status ? state with { Reports = action.Reports } : state;

    /// <summary>Resolving.</summary>
    [ReducerMethod]
    public static ModerationState OnResolve(ModerationState state, ResolveReport action) => state with { Resolving = action.ReportId };

    /// <summary>Resolved.</summary>
    [ReducerMethod(typeof(ReportResolved))]
    public static ModerationState OnResolved(ModerationState state) => state with { Resolving = null };
}

/// <summary>Side effects of the moderation queue.</summary>
public sealed class ModerationEffects(ILibraryApi api, IState<ModerationState> state)
{
    /// <summary>First page of reports.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadReports action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Dispatch(new ReportsLoaded(action.Status, await api.ListReportsAsync(action.Status, 1) switch
        {
            ApiResult<PagedResult<ReportDto>>.Success { Value: var page } => new Loadable<PagedList<ReportDto>>.Loaded(PagedList.From(page)),
            ApiResult<PagedResult<ReportDto>>.Failure { Error: var error } => new Loadable<PagedList<ReportDto>>.Failed(error),
            _ => throw new InvalidOperationException(),
        }));
    }

    /// <summary>Decides a report and reloads the queue; 409 means somebody decided it already.</summary>
    [EffectMethod]
    public async Task HandleResolve(ResolveReport action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await api.ResolveReportAsync(action.ReportId, action.Request);
        var error = result is ApiResult.Failure { Error: var failure } ? failure : null;
        dispatcher.Dispatch(new ReportResolved(action.ReportId, error));
        dispatcher.Dispatch(new ShowToast(error switch
        {
            null => Toast.Success(action.Request.Action == LibraryCodes.Remove ? "Content removed" : "Report rejected"),
            { Kind: ApiErrorKind.Conflict } => Toast.Info("This report was already decided."),
            _ => Toast.Error(error),
        }));
        dispatcher.Dispatch(new LoadReports(state.Value.Status));
    }
}
