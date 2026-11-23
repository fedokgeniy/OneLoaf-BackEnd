using System.Diagnostics.CodeAnalysis;
using OneLoaf.Web.Shared.Api;

namespace OneLoaf.Web.Shared.State;

/// <summary>An async value in state: <see cref="NotAsked"/>, <see cref="Loading"/>, <see cref="Loaded"/> or <see cref="Failed"/>.</summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory members keep reducers short: Loadable<T>.Idle.")]
public abstract record Loadable<T>
{
    private Loadable()
    {
    }

    /// <summary>Nothing requested yet.</summary>
    public static Loadable<T> Idle { get; } = new NotAsked();

    /// <summary>The last loaded value, kept while reloading so the view does not flash.</summary>
    public T? Value => this switch
    {
        Loaded loaded => loaded.Data,
        Loading { Previous: var previous } => previous,
        _ => default,
    };

    /// <summary>Nothing requested yet.</summary>
    public sealed record NotAsked : Loadable<T>;

    /// <summary>A request runs; <paramref name="Previous"/> is the value shown until it returns.</summary>
    public sealed record Loading(T? Previous) : Loadable<T>;

    /// <summary>The value arrived.</summary>
    public sealed record Loaded(T Data) : Loadable<T>;

    /// <summary>The request failed.</summary>
    public sealed record Failed(ApiError Error) : Loadable<T>;

    /// <summary>Starts a reload that keeps the current value visible.</summary>
    public Loadable<T> Reload() => new Loading(Value);
}
