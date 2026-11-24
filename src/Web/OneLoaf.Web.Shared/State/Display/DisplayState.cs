using Fluxor;
using OneLoaf.Web.Shared.Device;

namespace OneLoaf.Web.Shared.State.Display;

/// <summary>Theme chosen in Settings → Display.</summary>
public enum ThemePreference
{
    /// <summary>Follow <c>prefers-color-scheme</c>.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>User display settings (non-sensitive; stored in <c>localStorage</c> by <c>device.js</c>).</summary>
public sealed record DisplayPreferences(ThemePreference Theme, int Scale, bool ReduceMotion)
{
    /// <summary>Allowed display scale steps in percent.</summary>
    public static readonly IReadOnlyList<int> Scales = [90, 100, 125, 150];

    /// <summary>Defaults: system theme, 100 %, motion as the system says.</summary>
    public static DisplayPreferences Default { get; } = new(ThemePreference.System, 100, false);
}

/// <summary>Global display slice: user preferences and what the device reports.</summary>
[FeatureState(Name = "Display")]
public sealed record DisplayState(DisplayPreferences Preferences, DeviceInfo Device)
{
    private DisplayState()
        : this(DisplayPreferences.Default, DeviceInfo.Unknown)
    {
    }
}

/// <summary>Preferences read from storage at start-up.</summary>
public sealed record DisplayPreferencesLoaded(DisplayPreferences Preferences);

/// <summary>Intent: change the theme.</summary>
public sealed record SetTheme(ThemePreference Theme);

/// <summary>Intent: change the display scale (90, 100, 125, 150).</summary>
public sealed record SetScale(int Scale);

/// <summary>Intent: switch reduced motion on or off.</summary>
public sealed record SetReduceMotion(bool ReduceMotion);

/// <summary>The device reported a new size class, orientation or preference.</summary>
public sealed record DeviceChanged(DeviceInfo Device);

/// <summary>Pure reducers of <see cref="DisplayState"/>.</summary>
public static class DisplayReducers
{
    /// <summary>Stored preferences.</summary>
    [ReducerMethod]
    public static DisplayState OnLoaded(DisplayState state, DisplayPreferencesLoaded action) =>
        state with { Preferences = action.Preferences };

    /// <summary>Theme.</summary>
    [ReducerMethod]
    public static DisplayState OnSetTheme(DisplayState state, SetTheme action) =>
        state with { Preferences = state.Preferences with { Theme = action.Theme } };

    /// <summary>Scale; unknown steps are ignored.</summary>
    [ReducerMethod]
    public static DisplayState OnSetScale(DisplayState state, SetScale action) =>
        DisplayPreferences.Scales.Contains(action.Scale)
            ? state with { Preferences = state.Preferences with { Scale = action.Scale } }
            : state;

    /// <summary>Reduced motion.</summary>
    [ReducerMethod]
    public static DisplayState OnSetReduceMotion(DisplayState state, SetReduceMotion action) =>
        state with { Preferences = state.Preferences with { ReduceMotion = action.ReduceMotion } };

    /// <summary>Device snapshot.</summary>
    [ReducerMethod]
    public static DisplayState OnDeviceChanged(DisplayState state, DeviceChanged action) =>
        state with { Device = action.Device };
}

/// <summary>Applies changed preferences to the page (<c>data-theme</c>, <c>data-scale</c>, <c>data-motion</c>).</summary>
public sealed class DisplayEffects(IState<DisplayState> state, DeviceContext device)
{
    /// <summary>Theme changed.</summary>
    [EffectMethod(typeof(SetTheme))]
    public Task OnSetTheme(IDispatcher dispatcher) => device.ApplyDisplayAsync(state.Value.Preferences);

    /// <summary>Scale changed.</summary>
    [EffectMethod(typeof(SetScale))]
    public Task OnSetScale(IDispatcher dispatcher) => device.ApplyDisplayAsync(state.Value.Preferences);

    /// <summary>Reduced motion changed.</summary>
    [EffectMethod(typeof(SetReduceMotion))]
    public Task OnSetReduceMotion(IDispatcher dispatcher) => device.ApplyDisplayAsync(state.Value.Preferences);
}
