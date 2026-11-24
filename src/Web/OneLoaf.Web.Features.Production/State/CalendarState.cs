using System.Globalization;
using Fluxor;
using OneLoaf.Web.Features.Production.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Production.State;

/// <summary>An ISO week (Monday first), e.g. 2026-W48.</summary>
public sealed record CalendarWeek(int Year, int Week)
{
    /// <summary>Monday of the week, local midnight.</summary>
    public DateTimeOffset Start
    {
        get
        {
            var monday = ISOWeek.ToDateTime(Year, Week, DayOfWeek.Monday);
            return new DateTimeOffset(monday, TimeZoneInfo.Local.GetUtcOffset(monday));
        }
    }

    /// <summary>Monday of the next week.</summary>
    public DateTimeOffset End => Start.AddDays(7);

    /// <summary>"2026-W48".</summary>
    public string Code => string.Create(CultureInfo.InvariantCulture, $"{Year}-W{Week:00}");

    /// <summary>The week containing <paramref name="moment"/>.</summary>
    public static CalendarWeek Of(DateTimeOffset moment)
    {
        var local = moment.ToLocalTime().DateTime;
        return new CalendarWeek(ISOWeek.GetYear(local), ISOWeek.GetWeekOfYear(local));
    }

    /// <summary>Parses "2026-W48"; null when malformed.</summary>
    public static CalendarWeek? Parse(string? code)
    {
        if (code is not { Length: 8 } || code[4] != '-' || code[5] != 'W'
            || !int.TryParse(code.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(code.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var week)
            || week < 1 || week > ISOWeek.GetWeeksInYear(year))
        {
            return null;
        }

        return new CalendarWeek(year, week);
    }

    /// <summary>The week <paramref name="weeks"/> later (negative: earlier).</summary>
    public CalendarWeek Shift(int weeks) => Of(Start.AddDays(7 * weeks + 3));
}

/// <summary>Batches and scheduled reminders of a week; <see cref="RemindersError"/> when the Notification service failed.</summary>
public sealed record WeekPlan(IReadOnlyList<BatchSummaryDto> Batches, IReadOnlyList<ReminderDto> Reminders, ApiError? RemindersError);

/// <summary>Calendar slice (US-26).</summary>
[FeatureState(Name = "Calendar")]
public sealed record CalendarState(CalendarWeek? Week, Loadable<WeekPlan> Plan)
{
    private CalendarState()
        : this(null, Loadable<WeekPlan>.Idle)
    {
    }
}

/// <summary>Intent: show a week.</summary>
public sealed record LoadWeek(CalendarWeek Week);

/// <summary>Result: week loaded.</summary>
public sealed record WeekLoaded(CalendarWeek Week, Loadable<WeekPlan> Plan);

/// <summary>Reducers of <see cref="CalendarState"/>.</summary>
public static class CalendarReducers
{
    /// <summary>Loading; the same week keeps showing while it reloads.</summary>
    [ReducerMethod]
    public static CalendarState OnLoad(CalendarState state, LoadWeek action) =>
        new(action.Week, action.Week == state.Week ? state.Plan.Reload() : new Loadable<WeekPlan>.Loading(null));

    /// <summary>Result for the shown week.</summary>
    [ReducerMethod]
    public static CalendarState OnLoaded(CalendarState state, WeekLoaded action) =>
        action.Week == state.Week ? state with { Plan = action.Plan } : state;

    /// <summary>A new batch or a status change makes the week stale.</summary>
    [ReducerMethod(typeof(BatchCreated))]
    public static CalendarState OnBatchCreated(CalendarState state) => state with { Plan = Loadable<WeekPlan>.Idle };

    /// <summary>Completed batches change their status.</summary>
    [ReducerMethod(typeof(BatchCompleted))]
    public static CalendarState OnBatchCompleted(CalendarState state) => state with { Plan = Loadable<WeekPlan>.Idle };

    /// <summary>Cancelled batches change their status.</summary>
    [ReducerMethod(typeof(BatchDeleted))]
    public static CalendarState OnBatchDeleted(CalendarState state) => state with { Plan = Loadable<WeekPlan>.Idle };

    /// <summary>A cancelled batch changes the week.</summary>
    [ReducerMethod(typeof(BatchCancelled))]
    public static CalendarState OnBatchCancelled(CalendarState state) => state with { Plan = Loadable<WeekPlan>.Idle };
}

/// <summary>Loads a week from the Core API and the Notification service in parallel.</summary>
public sealed class CalendarEffects(IProductionApi api)
{
    /// <summary>The reminders are optional: when the Notification service fails, the batches still show.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadWeek action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var batches = api.ListBatchesAsync(action.Week.Start, action.Week.End, null);
        var reminders = api.ListRemindersAsync(action.Week.Start, action.Week.End);
        var batchResult = await batches;
        var reminderResult = await reminders;
        dispatcher.Dispatch(new WeekLoaded(action.Week, batchResult switch
        {
            ApiResult<PagedResult<BatchSummaryDto>>.Success { Value: var page } => new Loadable<WeekPlan>.Loaded(new WeekPlan(
                page.Items,
                reminderResult is ApiResult<IReadOnlyList<ReminderDto>>.Success { Value: var list } ? list : [],
                (reminderResult as ApiResult<IReadOnlyList<ReminderDto>>.Failure)?.Error)),
            ApiResult<PagedResult<BatchSummaryDto>>.Failure { Error: var error } => new Loadable<WeekPlan>.Failed(error),
            _ => throw new InvalidOperationException(),
        }));
    }
}
