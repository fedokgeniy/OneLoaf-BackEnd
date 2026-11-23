using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Shared.Api;

namespace OneLoaf.Web.Shared.State.Notifications;

/// <summary>Kind of a toast message.</summary>
public enum ToastKind
{
    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Something was saved or done.</summary>
    Success,

    /// <summary>Attention needed soon.</summary>
    Warning,

    /// <summary>An action failed; stays until closed.</summary>
    Error,
}

/// <summary>A short message shown by <see cref="OlToastHost"/>.</summary>
public sealed record Toast(
    Guid Id,
    ToastKind Kind,
    string Message,
    string? CorrelationId = null,
    string? ActionText = null,
    string? ActionHref = null)
{
    /// <summary>Errors stay until the user closes them; other toasts disappear after <see cref="AutoDismissAfter"/>.</summary>
    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(6);

    /// <summary>Creates an information toast.</summary>
    public static Toast Info(string message) => new(Guid.NewGuid(), ToastKind.Info, message);

    /// <summary>Creates a success toast.</summary>
    public static Toast Success(string message, string? actionText = null, string? actionHref = null) =>
        new(Guid.NewGuid(), ToastKind.Success, message, null, actionText, actionHref);

    /// <summary>Creates a warning toast.</summary>
    public static Toast Warning(string message) => new(Guid.NewGuid(), ToastKind.Warning, message);

    /// <summary>Creates an error toast from an API error (detail + correlation id).</summary>
    public static Toast Error(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Toast(Guid.NewGuid(), ToastKind.Error, error.Message, error.CorrelationId);
    }

    /// <summary>The toast closes itself.</summary>
    public bool AutoDismiss => Kind != ToastKind.Error;
}

/// <summary>Global slice of toast messages, oldest first.</summary>
[FeatureState(Name = "Notifications")]
public sealed record NotificationsState(ImmutableList<Toast> Toasts)
{
    private NotificationsState()
        : this(ImmutableList<Toast>.Empty)
    {
    }
}

/// <summary>Intent: show a toast.</summary>
public sealed record ShowToast(Toast Toast);

/// <summary>Intent: close a toast.</summary>
public sealed record DismissToast(Guid Id);

/// <summary>Pure reducers of <see cref="NotificationsState"/>.</summary>
public static class NotificationsReducers
{
    /// <summary>Same message twice in a row is shown once.</summary>
    [ReducerMethod]
    public static NotificationsState OnShow(NotificationsState state, ShowToast action) =>
        state.Toasts.Any(t => t.Message == action.Toast.Message && t.Kind == action.Toast.Kind)
            ? state
            : state with { Toasts = state.Toasts.Add(action.Toast) };

    /// <summary>Removes the toast.</summary>
    [ReducerMethod]
    public static NotificationsState OnDismiss(NotificationsState state, DismissToast action) =>
        state with { Toasts = state.Toasts.RemoveAll(t => t.Id == action.Id) };
}

/// <summary>Closes non-error toasts after <see cref="Toast.AutoDismissAfter"/>.</summary>
public sealed class NotificationsEffects(TimeProvider time)
{
    /// <summary>Schedules the auto-dismiss.</summary>
    [EffectMethod]
    public async Task OnShow(ShowToast action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!action.Toast.AutoDismiss)
        {
            return;
        }

        await Task.Delay(Toast.AutoDismissAfter, time);
        dispatcher.Dispatch(new DismissToast(action.Toast.Id));
    }
}
