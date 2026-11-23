using Fluxor;
using Microsoft.JSInterop;
using OneLoaf.Web.Shared.State.Connectivity;
using OneLoaf.Web.Shared.State.Display;

namespace OneLoaf.Web.Shared.Device;

/// <summary>
/// Bridge to <c>wwwroot/js/device.js</c>: reports size class, orientation, pointer, system preferences and connectivity
/// into the <see cref="DisplayState"/> and <see cref="ConnectivityState"/> slices, and applies display preferences.
/// Only primitives cross the interop boundary (trimming-safe).
/// </summary>
public sealed class DeviceContext(IJSRuntime js, IDispatcher dispatcher) : IAsyncDisposable
{
    private const string ModulePath = "./js/device.js";
    private IJSObjectReference? _module;
    private DotNetObjectReference<DeviceContext>? _self;

    /// <summary>Loads the module and starts listening; the first reports arrive right after.</summary>
    public async Task StartAsync()
    {
        if (_module is not null)
        {
            return;
        }

        _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _self = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("start", _self);
    }

    /// <summary>Writes the preferences to <c>&lt;html&gt;</c> and <c>localStorage</c>.</summary>
    public async Task ApplyDisplayAsync(DisplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (_module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("applyDisplay",
            preferences.Theme.ToString().ToLowerInvariant(), preferences.Scale, preferences.ReduceMotion);
    }

    /// <summary>Called by <c>device.js</c> on start and on every media query change.</summary>
    [JSInvokable]
    public void OnDeviceChanged(
        string sizeClass, bool landscape, bool coarsePointer, bool canHover, bool prefersDark,
        bool prefersReducedMotion, bool twoSegments, double pixelRatio) =>
        dispatcher.Dispatch(new DeviceChanged(new DeviceInfo(
            DeviceInfo.ParseSizeClass(sizeClass), landscape, coarsePointer, canHover, prefersDark,
            prefersReducedMotion, twoSegments, pixelRatio)));

    /// <summary>Called by <c>device.js</c> on start and on <c>online</c> / <c>offline</c> events.</summary>
    [JSInvokable]
    public void OnConnectivityChanged(bool online) => dispatcher.Dispatch(new ConnectivityChanged(online));

    /// <summary>Called by <c>device.js</c> on start with the stored display preferences.</summary>
    [JSInvokable]
    public void OnDisplayLoaded(string theme, int scale, bool reduceMotion)
    {
        var parsed = Enum.TryParse<ThemePreference>(theme, ignoreCase: true, out var value) ? value : ThemePreference.System;
        dispatcher.Dispatch(new DisplayPreferencesLoaded(new DisplayPreferences(parsed, scale, reduceMotion)));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("stop");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is closing.
            }
        }

        _self?.Dispose();
    }
}
