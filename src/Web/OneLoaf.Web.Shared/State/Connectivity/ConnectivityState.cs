using Fluxor;

namespace OneLoaf.Web.Shared.State.Connectivity;

/// <summary>Global slice: is the browser online (<c>navigator.onLine</c>, reported by <c>device.js</c>).</summary>
[FeatureState(Name = "Connectivity")]
public sealed record ConnectivityState(bool IsOnline)
{
    private ConnectivityState()
        : this(true)
    {
    }
}

/// <summary>The browser went online or offline.</summary>
public sealed record ConnectivityChanged(bool IsOnline);

/// <summary>Pure reducers of <see cref="ConnectivityState"/>.</summary>
public static class ConnectivityReducers
{
    /// <summary>Stores the new value.</summary>
    [ReducerMethod]
    public static ConnectivityState OnChanged(ConnectivityState state, ConnectivityChanged action) =>
        state with { IsOnline = action.IsOnline };
}
