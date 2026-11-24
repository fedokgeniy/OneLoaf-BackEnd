using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Reference;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Kitchen settings and the currencies to choose from; <see cref="Settings"/> is null before the first save.</summary>
public sealed record KitchenData(KitchenSettingsDto? Settings, IReadOnlyList<CurrencyDto> Currencies);

/// <summary>Kitchen settings slice (US-13, US-47–49).</summary>
[FeatureState(Name = "Kitchen")]
public sealed record KitchenState(Loadable<KitchenData> Data, bool Busy)
{
    private KitchenState()
        : this(Loadable<KitchenData>.Idle, false)
    {
    }
}

/// <summary>Intent: load the settings.</summary>
public sealed record LoadKitchen;

/// <summary>Result: settings loaded or failed.</summary>
public sealed record KitchenLoaded(Loadable<KitchenData> Data);

/// <summary>Intent: save the settings.</summary>
public sealed record SaveKitchen(KitchenSettingsRequest Request);

/// <summary>Result: saved.</summary>
public sealed record KitchenSaved(KitchenSettingsDto Settings);

/// <summary>Result: saving failed.</summary>
public sealed record KitchenSaveFailed(ApiError Error);

/// <summary>Reducers of <see cref="KitchenState"/>.</summary>
public static class KitchenReducers
{
    /// <summary>Request starts.</summary>
    [ReducerMethod(typeof(LoadKitchen))]
    public static KitchenState OnLoad(KitchenState state) => state with { Data = state.Data.Reload() };

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static KitchenState OnLoaded(KitchenState state, KitchenLoaded action) => state with { Data = action.Data };

    /// <summary>Saving.</summary>
    [ReducerMethod(typeof(SaveKitchen))]
    public static KitchenState OnSave(KitchenState state) => state with { Busy = true };

    /// <summary>Saved.</summary>
    [ReducerMethod]
    public static KitchenState OnSaved(KitchenState state, KitchenSaved action) =>
        new(state.Data.Value is { } data ? new Loadable<KitchenData>.Loaded(data with { Settings = action.Settings }) : state.Data, false);

    /// <summary>Save failed.</summary>
    [ReducerMethod(typeof(KitchenSaveFailed))]
    public static KitchenState OnSaveFailed(KitchenState state) => state with { Busy = false };

    /// <summary>Cost and kneading figures depend on the kitchen: the recipe detail recalculates on its next visit.</summary>
    [ReducerMethod(typeof(KitchenSaved))]
    public static RecipeDetailState DetailAfterKitchen(RecipeDetailState state) => RecipeDetailState.Initial;
}

/// <summary>Side effects of the kitchen slice.</summary>
public sealed class KitchenEffects(ICatalogApi api, IReferenceApi reference)
{
    /// <summary>Loads settings and currencies; 404 means "not saved yet".</summary>
    [EffectMethod(typeof(LoadKitchen))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        var settings = api.GetKitchenAsync();
        var currencies = reference.GetCurrenciesAsync();
        var settingsResult = await settings;
        var currenciesResult = await currencies;
        if (settingsResult is ApiResult<KitchenSettingsDto>.Failure { Error.Kind: not ApiErrorKind.NotFound } failed)
        {
            dispatcher.Dispatch(new KitchenLoaded(new Loadable<KitchenData>.Failed(failed.Error)));
            return;
        }

        var list = currenciesResult is ApiResult<IReadOnlyList<CurrencyDto>>.Success { Value: var c } ? c : [];
        dispatcher.Dispatch(new KitchenLoaded(new Loadable<KitchenData>.Loaded(
            new KitchenData((settingsResult as ApiResult<KitchenSettingsDto>.Success)?.Value, list))));
    }

    /// <summary>Saves.</summary>
    [EffectMethod]
    public async Task HandleSave(SaveKitchen action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.SaveKitchenAsync(action.Request))
        {
            case ApiResult<KitchenSettingsDto>.Success { Value: var settings }:
                dispatcher.Dispatch(new KitchenSaved(settings));
                dispatcher.Dispatch(new ShowToast(Toast.Success("My kitchen saved")));
                break;
            case ApiResult<KitchenSettingsDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new KitchenSaveFailed(error));
                break;
        }
    }
}
