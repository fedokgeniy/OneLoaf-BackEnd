using System.Collections.Concurrent;
using System.Reflection;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;

namespace OneLoaf.Web.Shared.State;

/// <summary>
/// Entry point of a lazy-loaded feature assembly (ADR-F04): the services its effects need beyond those registered at
/// startup (typically its typed API service), created when the assembly is loaded.
/// </summary>
public interface IFeatureModule
{
    /// <summary>Instances handed to the feature's effect classes, e.g. <c>ActivatorUtilities.CreateInstance&lt;HistoryApi&gt;(services)</c>.</summary>
    IReadOnlyList<object> CreateServices(IServiceProvider services);
}

/// <summary>
/// Adds the Fluxor features, reducers and effects of a lazy-loaded assembly to the running store, with the same
/// attributes the startup scan uses (<see cref="FeatureStateAttribute"/>, <see cref="ReducerMethodAttribute"/>,
/// <see cref="EffectMethodAttribute"/>). Each assembly is registered once.
/// </summary>
public sealed class LazyFeatureRegistry(IStore store, IServiceProvider services)
{
    private readonly ConcurrentDictionary<Assembly, bool> _registered = new();

    /// <summary>True when the assembly was registered.</summary>
    public bool IsRegistered(Assembly assembly) => _registered.ContainsKey(assembly);

    /// <summary>Registers the assembly's features, then reducers, then effects.</summary>
    public void Register(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!_registered.TryAdd(assembly, true))
        {
            return;
        }

        var types = assembly.GetTypes();
        var features = new Dictionary<Type, IFeature>();
        foreach (var stateType in types.Where(t => t.GetCustomAttribute<FeatureStateAttribute>() is not null))
        {
            var feature = (IFeature)Activator.CreateInstance(typeof(RuntimeFeature<>).MakeGenericType(stateType), stateType.GetCustomAttribute<FeatureStateAttribute>())!;
            features[stateType] = feature;
        }

        foreach (var method in types.Where(t => t.IsAbstract && t.IsSealed).SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)))
        {
            if (method.GetCustomAttribute<ReducerMethodAttribute>() is not { } attribute)
            {
                continue;
            }

            var stateType = method.ReturnType;
            var actionType = attribute.ActionType ?? method.GetParameters()[1].ParameterType;
            var feature = features.TryGetValue(stateType, out var own) ? own : store.Features.Values.First(f => f.GetStateType() == stateType);
            var reducer = Activator.CreateInstance(typeof(MethodReducer<>).MakeGenericType(stateType), method, actionType)!;
            typeof(IFeature<>).MakeGenericType(stateType).GetMethod(nameof(IFeature<object>.AddReducer))!.Invoke(feature, [reducer]);
        }

        foreach (var feature in features.Values)
        {
            store.AddFeature(feature);
        }

        var extras = assembly.GetTypes()
            .Where(t => typeof(IFeatureModule).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => ((IFeatureModule)ActivatorUtilities.CreateInstance(services, t)).CreateServices(services))
            .ToArray();
        foreach (var effectClass in types.Where(t => t is { IsClass: true, IsAbstract: false }
                     && t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.GetCustomAttribute<EffectMethodAttribute>() is not null)))
        {
            var instance = ActivatorUtilities.CreateInstance(services, effectClass, extras);
            foreach (var method in effectClass.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<EffectMethodAttribute>() is { } attribute)
                {
                    store.AddEffect((IEffect)Activator.CreateInstance(typeof(MethodEffect<>).MakeGenericType(effectClass), instance, method,
                        attribute.ActionType ?? method.GetParameters()[0].ParameterType)!);
                }
            }
        }
    }

    private sealed class RuntimeFeature<TState> : Feature<TState>
    {
        private readonly string _name;

        public RuntimeFeature(FeatureStateAttribute attribute)
        {
            _name = attribute.Name ?? typeof(TState).FullName!;
        }

        public override string GetName() => _name;

        protected override TState GetInitialState() => (TState)Activator.CreateInstance(typeof(TState), nonPublic: true)!;
    }

    private sealed class MethodReducer<TState>(MethodInfo method, Type actionType) : IReducer<TState>
    {
        private readonly bool _withAction = method.GetParameters().Length == 2;

        public bool ShouldReduceStateForAction(object action) => actionType.IsInstanceOfType(action);

        public TState Reduce(TState state, object action) =>
            (TState)method.Invoke(null, _withAction ? [state, action] : [state])!;
    }

    // Generic so that Fluxor's startup scan of this assembly does not take it for an effect class to resolve from DI.
    private sealed class MethodEffect<TEffects>(object instance, MethodInfo method, Type actionType) : IEffect
    {
        private readonly bool _withAction = method.GetParameters().Length == 2;

        public bool ShouldReactToAction(object action) => actionType.IsInstanceOfType(action);

        public Task HandleAsync(object action, IDispatcher dispatcher) =>
            (Task)method.Invoke(instance, _withAction ? [action, dispatcher] : [dispatcher])!;
    }
}

/// <summary>
/// <see cref="IState{TState}"/> registered as the open generic after Fluxor's own: it serves both the states found by
/// the startup scan and those of lazy-loaded features (added by <see cref="LazyFeatureRegistry"/>). The store is looked
/// up on first use, because effects that take an <c>IState</c> are created while the store itself is being built.
/// </summary>
public sealed class LazyState<TState>(IServiceProvider services) : IState<TState>
{
    private Fluxor.State<TState>? _inner;

    /// <inheritdoc />
    public event EventHandler? StateChanged
    {
        add => Inner.StateChanged += value;
        remove => Inner.StateChanged -= value;
    }

    /// <inheritdoc />
    public TState Value => Inner.Value;

    private Fluxor.State<TState> Inner => _inner ??= new(
        (IFeature<TState>)(services.GetRequiredService<IStore>().Features.Values.FirstOrDefault(f => f.GetStateType() == typeof(TState))
            ?? throw new InvalidOperationException($"The feature of {typeof(TState).Name} is not loaded.")));
}
