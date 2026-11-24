using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Reference;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Reference data the ingredient screens need, loaded once.</summary>
public sealed record CatalogReference(
    IReadOnlyList<IngredientCategoryDto> Categories,
    IReadOnlyList<UnitDto> Units,
    IReadOnlyList<CurrencyDto> Currencies,
    IReadOnlyList<FlourDto> Flours,
    IReadOnlyList<FlourTypeDto> FlourTypes);

/// <summary>
/// Ingredients slice (US-10–12): list with search and category, the selected ingredient's price history and stock ledger.
/// </summary>
[FeatureState(Name = "Catalog")]
public sealed record CatalogState(
    string Search,
    string? Category,
    Loadable<PagedResult<IngredientDto>> List,
    Loadable<CatalogReference> Reference,
    Guid? SelectedId,
    Loadable<IReadOnlyList<PriceDto>> Prices,
    Loadable<PagedResult<StockMovementDto>> Movements,
    bool Busy)
{
    private CatalogState()
        : this(string.Empty, null, Loadable<PagedResult<IngredientDto>>.Idle, Loadable<CatalogReference>.Idle, null,
            Loadable<IReadOnlyList<PriceDto>>.Idle, Loadable<PagedResult<StockMovementDto>>.Idle, false)
    {
    }
}

/// <summary>Intent: load the list for a search and category (debounce is done by the page).</summary>
public sealed record LoadIngredients(string Search, string? Category);

/// <summary>Result: list.</summary>
public sealed record IngredientsLoaded(string Search, string? Category, Loadable<PagedResult<IngredientDto>> List);

/// <summary>Result: reference data.</summary>
public sealed record CatalogReferenceLoaded(Loadable<CatalogReference> Reference);

/// <summary>Intent: show an ingredient and its price history (null closes the detail).</summary>
public sealed record SelectIngredient(Guid? Id);

/// <summary>Result: price history.</summary>
public sealed record PricesLoaded(Guid Id, Loadable<IReadOnlyList<PriceDto>> Prices);

/// <summary>Intent: create (null id) or update an ingredient.</summary>
public sealed record SaveIngredient(Guid? Id, IngredientRequest Request);

/// <summary>Result: saved.</summary>
public sealed record IngredientSaved(IngredientDto Ingredient);

/// <summary>Intent: delete an ingredient.</summary>
public sealed record DeleteIngredient(Guid Id);

/// <summary>Result: deleted.</summary>
public sealed record IngredientDeleted(Guid Id);

/// <summary>Intent: add a price (US-12).</summary>
public sealed record AddPrice(Guid IngredientId, PriceRequest Request);

/// <summary>Result: price added.</summary>
public sealed record PriceAdded(Guid IngredientId, PriceDto Price);

/// <summary>Intent: replace a price entered by mistake.</summary>
public sealed record UpdatePrice(Guid IngredientId, Guid PriceId, PriceRequest Request);

/// <summary>Intent: delete a price.</summary>
public sealed record DeletePrice(Guid IngredientId, Guid PriceId);

/// <summary>Result: a price was replaced or deleted.</summary>
public sealed record PriceChanged(Guid IngredientId);

/// <summary>Result: the stock ledger of the selected ingredient.</summary>
public sealed record MovementsLoaded(Guid Id, Loadable<PagedResult<StockMovementDto>> Movements);

/// <summary>Intent: record a purchase (packs of a size for an amount).</summary>
public sealed record AddPurchase(Guid IngredientId, PurchaseRequest Request);

/// <summary>Intent: set the stock to the counted quantity.</summary>
public sealed record AdjustStock(Guid IngredientId, StockAdjustmentRequest Request);

/// <summary>Result: the stock changed (purchase or correction).</summary>
public sealed record StockChanged(Guid IngredientId);

/// <summary>Intent: create (null id) or update a flour of the catalog (US-11).</summary>
public sealed record SaveFlour(Guid? Id, FlourRequest Request);

/// <summary>Result: flour saved.</summary>
public sealed record FlourSaved(FlourDto Flour, bool Created);

/// <summary>Intent: delete a flour.</summary>
public sealed record DeleteFlour(Guid Id);

/// <summary>Result: flour deleted.</summary>
public sealed record FlourDeleted(Guid Id);

/// <summary>Result: a catalog command failed; forms map field errors.</summary>
public sealed record CatalogCommandFailed(string Command, ApiError Error);

/// <summary>Reducers of <see cref="CatalogState"/>.</summary>
public static class CatalogReducers
{
    /// <summary>A recipe saved elsewhere (assistant proposal) may have added ingredients: the list reloads on the next visit.</summary>
    [ReducerMethod(typeof(RecipesChanged))]
    public static CatalogState OnRecipesChanged(CatalogState state) =>
        state with { List = Loadable<PagedResult<IngredientDto>>.Idle, SelectedId = null };

    /// <summary>List request.</summary>
    [ReducerMethod]
    public static CatalogState OnLoad(CatalogState state, LoadIngredients action) =>
        state with { Search = action.Search, Category = action.Category, List = state.List.Reload() };

    /// <summary>List result for the current filter.</summary>
    [ReducerMethod]
    public static CatalogState OnLoaded(CatalogState state, IngredientsLoaded action) =>
        action.Search == state.Search && action.Category == state.Category ? state with { List = action.List } : state;

    /// <summary>Reference data.</summary>
    [ReducerMethod]
    public static CatalogState OnReference(CatalogState state, CatalogReferenceLoaded action) => state with { Reference = action.Reference };

    /// <summary>Selection.</summary>
    [ReducerMethod]
    public static CatalogState OnSelect(CatalogState state, SelectIngredient action) =>
        state with
        {
            SelectedId = action.Id,
            Prices = action.Id is null ? Loadable<IReadOnlyList<PriceDto>>.Idle
                : action.Id == state.SelectedId ? state.Prices.Reload() : new Loadable<IReadOnlyList<PriceDto>>.Loading(null),
            Movements = action.Id is null ? Loadable<PagedResult<StockMovementDto>>.Idle
                : action.Id == state.SelectedId ? state.Movements.Reload() : new Loadable<PagedResult<StockMovementDto>>.Loading(null),
        };

    /// <summary>Stock ledger of the selected ingredient.</summary>
    [ReducerMethod]
    public static CatalogState OnMovements(CatalogState state, MovementsLoaded action) =>
        action.Id == state.SelectedId ? state with { Movements = action.Movements } : state;

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(UpdatePrice))]
    public static CatalogState OnUpdatePrice(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(DeletePrice))]
    public static CatalogState OnDeletePrice(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(AddPurchase))]
    public static CatalogState OnAddPurchase(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(AdjustStock))]
    public static CatalogState OnAdjustStock(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(SaveFlour))]
    public static CatalogState OnSaveFlour(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(DeleteFlour))]
    public static CatalogState OnDeleteFlour(CatalogState state) => state with { Busy = true };

    /// <summary>Command finished.</summary>
    [ReducerMethod(typeof(PriceChanged))]
    public static CatalogState OnPriceChanged(CatalogState state) => state with { Busy = false };

    /// <summary>Command finished.</summary>
    [ReducerMethod(typeof(StockChanged))]
    public static CatalogState OnStockChanged(CatalogState state) => state with { Busy = false };

    /// <summary>The saved flour joins or replaces its row of the reference data, ordered by name.</summary>
    [ReducerMethod]
    public static CatalogState OnFlourSaved(CatalogState state, FlourSaved action) =>
        state with
        {
            Busy = false,
            Reference = state.Reference.Value is { } reference
                ? new Loadable<CatalogReference>.Loaded(reference with
                {
                    Flours = reference.Flours.Where(f => f.Id != action.Flour.Id).Append(action.Flour)
                        .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                })
                : state.Reference,
        };

    /// <summary>The deleted flour leaves the reference data.</summary>
    [ReducerMethod]
    public static CatalogState OnFlourDeleted(CatalogState state, FlourDeleted action) =>
        state with
        {
            Busy = false,
            Reference = state.Reference.Value is { } reference
                ? new Loadable<CatalogReference>.Loaded(reference with { Flours = reference.Flours.Where(f => f.Id != action.Id).ToList() })
                : state.Reference,
        };

    /// <summary>Prices of the selected ingredient.</summary>
    [ReducerMethod]
    public static CatalogState OnPrices(CatalogState state, PricesLoaded action) =>
        action.Id == state.SelectedId ? state with { Prices = action.Prices } : state;

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(SaveIngredient))]
    public static CatalogState OnSave(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(DeleteIngredient))]
    public static CatalogState OnDelete(CatalogState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(AddPrice))]
    public static CatalogState OnAddPrice(CatalogState state) => state with { Busy = true };

    /// <summary>Saved: select it.</summary>
    [ReducerMethod]
    public static CatalogState OnSaved(CatalogState state, IngredientSaved action) =>
        state with { Busy = false, SelectedId = action.Ingredient.Id };

    /// <summary>Deleted: close the detail.</summary>
    [ReducerMethod]
    public static CatalogState OnDeleted(CatalogState state, IngredientDeleted action) =>
        state with { Busy = false, SelectedId = state.SelectedId == action.Id ? null : state.SelectedId };

    /// <summary>Price added.</summary>
    [ReducerMethod(typeof(PriceAdded))]
    public static CatalogState OnPriceAdded(CatalogState state) => state with { Busy = false };

    /// <summary>Command failed.</summary>
    [ReducerMethod(typeof(CatalogCommandFailed))]
    public static CatalogState OnFailed(CatalogState state) => state with { Busy = false };

    /// <summary>A new price changes recipe costs: the recipe detail recalculates on its next visit.</summary>
    [ReducerMethod(typeof(PriceAdded))]
    public static RecipeDetailState DetailAfterPrice(RecipeDetailState state) => RecipeDetailState.Initial;

    /// <summary>A replaced or deleted price changes recipe costs as well.</summary>
    [ReducerMethod(typeof(PriceChanged))]
    public static RecipeDetailState DetailAfterPriceChange(RecipeDetailState state) => RecipeDetailState.Initial;

    /// <summary>A purchase sets the price of its date.</summary>
    [ReducerMethod(typeof(StockChanged))]
    public static RecipeDetailState DetailAfterStockChange(RecipeDetailState state) => RecipeDetailState.Initial;

    /// <summary>The editor's ingredient list is stale after a catalog change.</summary>
    [ReducerMethod(typeof(IngredientSaved))]
    public static RecipeEditorState EditorAfterSave(RecipeEditorState state) => state with { RecipeId = null, Data = Loadable<EditorData>.Idle };
}

/// <summary>Side effects of the catalog slice.</summary>
public sealed class CatalogEffects(ICatalogApi api, IReferenceApi reference, IState<CatalogState> state)
{
    /// <summary>Loads the list (one page of 100) and, the first time, the reference data.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadIngredients action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Reference is Loadable<CatalogReference>.NotAsked or Loadable<CatalogReference>.Failed)
        {
            _ = LoadReference(dispatcher);
        }

        var result = await api.ListIngredientsAsync(action.Search, action.Category, 1, ICatalogApi.MaxPageSize);
        dispatcher.Dispatch(new IngredientsLoaded(action.Search, action.Category, result.ToLoadable()));
    }

    /// <summary>Loads the price history and the stock ledger of the selected ingredient.</summary>
    [EffectMethod]
    public async Task HandleSelect(SelectIngredient action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (action.Id is { } id)
        {
            var prices = api.GetPricesAsync(id);
            var movements = api.ListMovementsAsync(id);
            dispatcher.Dispatch(new PricesLoaded(id, (await prices).ToLoadable()));
            dispatcher.Dispatch(new MovementsLoaded(id, (await movements).ToLoadable()));
        }
    }

    /// <summary>Replaces a price; the history and the list (today's price) reload.</summary>
    [EffectMethod]
    public async Task HandleUpdatePrice(UpdatePrice action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.UpdatePriceAsync(action.IngredientId, action.PriceId, action.Request))
        {
            case ApiResult<PriceDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(UpdatePrice), error));
                break;
            default:
                AfterChange(dispatcher, new PriceChanged(action.IngredientId), "Price saved", action.IngredientId);
                break;
        }
    }

    /// <summary>Deletes a price.</summary>
    [EffectMethod]
    public async Task HandleDeletePrice(DeletePrice action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeletePriceAsync(action.IngredientId, action.PriceId))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(DeletePrice), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                AfterChange(dispatcher, new PriceChanged(action.IngredientId), "Price deleted", action.IngredientId);
                break;
        }
    }

    /// <summary>Records a purchase; stock, ledger, price history and list come from the server again.</summary>
    [EffectMethod]
    public async Task HandleAddPurchase(AddPurchase action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AddPurchaseAsync(action.IngredientId, action.Request))
        {
            case ApiResult<StockMovementDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(AddPurchase), error));
                break;
            default:
                AfterChange(dispatcher, new StockChanged(action.IngredientId), "Purchase recorded", action.IngredientId);
                break;
        }
    }

    /// <summary>Corrects the stock to the counted quantity.</summary>
    [EffectMethod]
    public async Task HandleAdjustStock(AdjustStock action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AdjustStockAsync(action.IngredientId, action.Request))
        {
            case ApiResult<StockMovementDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(AdjustStock), error));
                break;
            default:
                AfterChange(dispatcher, new StockChanged(action.IngredientId), "Stock corrected", action.IngredientId);
                break;
        }
    }

    /// <summary>Creates or updates a flour; ingredients show its name and protein, so the list reloads.</summary>
    [EffectMethod]
    public async Task HandleSaveFlour(SaveFlour action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = action.Id is { } id ? await api.UpdateFlourAsync(id, action.Request) : await api.CreateFlourAsync(action.Request);
        switch (result)
        {
            case ApiResult<FlourDto>.Success { Value: var flour }:
                dispatcher.Dispatch(new FlourSaved(flour, action.Id is null));
                dispatcher.Dispatch(new ShowToast(Toast.Success(action.Id is null ? "Flour added" : "Flour saved")));
                if (action.Id is not null)
                {
                    dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                }

                break;
            case ApiResult<FlourDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(SaveFlour), error));
                break;
        }
    }

    /// <summary>Deletes a flour; linked ingredients keep existing without it.</summary>
    [EffectMethod]
    public async Task HandleDeleteFlour(DeleteFlour action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeleteFlourAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(DeleteFlour), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new FlourDeleted(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Flour deleted")));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                break;
        }
    }

    private void AfterChange(IDispatcher dispatcher, object result, string message, Guid ingredientId)
    {
        dispatcher.Dispatch(result);
        dispatcher.Dispatch(new ShowToast(Toast.Success(message)));
        dispatcher.Dispatch(new SelectIngredient(ingredientId));
        dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
    }

    /// <summary>Creates or updates, then reloads the list and selects the ingredient.</summary>
    [EffectMethod]
    public async Task HandleSave(SaveIngredient action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = action.Id is { } id ? await api.UpdateIngredientAsync(id, action.Request) : await api.CreateIngredientAsync(action.Request);
        switch (result)
        {
            case ApiResult<IngredientDto>.Success { Value: var ingredient }:
                dispatcher.Dispatch(new IngredientSaved(ingredient));
                dispatcher.Dispatch(new ShowToast(Toast.Success(action.Id is null ? "Ingredient added" : "Ingredient saved")));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                dispatcher.Dispatch(new SelectIngredient(ingredient.Id));
                break;
            case ApiResult<IngredientDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(SaveIngredient), error));
                break;
        }
    }

    /// <summary>Deletes; 409 when a recipe uses the ingredient.</summary>
    [EffectMethod]
    public async Task HandleDelete(DeleteIngredient action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeleteIngredientAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(DeleteIngredient), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new IngredientDeleted(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Ingredient deleted")));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                break;
        }
    }

    /// <summary>Adds a price; the list (current price) and the history reload.</summary>
    [EffectMethod]
    public async Task HandleAddPrice(AddPrice action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AddPriceAsync(action.IngredientId, action.Request))
        {
            case ApiResult<PriceDto>.Success { Value: var price }:
                dispatcher.Dispatch(new PriceAdded(action.IngredientId, price));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Price added")));
                dispatcher.Dispatch(new SelectIngredient(action.IngredientId));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                break;
            case ApiResult<PriceDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(AddPrice), error));
                break;
        }
    }

    private async Task LoadReference(IDispatcher dispatcher)
    {
        var categories = reference.GetIngredientCategoriesAsync();
        var units = reference.GetUnitsAsync();
        var currencies = reference.GetCurrenciesAsync();
        var flours = api.ListFloursAsync();
        var flourTypes = reference.GetFlourTypesAsync();
        if (await categories is ApiResult<IReadOnlyList<IngredientCategoryDto>>.Success { Value: var c }
            && await units is ApiResult<IReadOnlyList<UnitDto>>.Success { Value: var u }
            && await currencies is ApiResult<IReadOnlyList<CurrencyDto>>.Success { Value: var cur }
            && await flours is ApiResult<PagedResult<FlourDto>>.Success { Value: var f }
            && await flourTypes is ApiResult<IReadOnlyList<FlourTypeDto>>.Success { Value: var types })
        {
            dispatcher.Dispatch(new CatalogReferenceLoaded(
                new Loadable<CatalogReference>.Loaded(new CatalogReference(c, u, cur, f.Items, types))));
            return;
        }

        var error = (await categories as ApiResult<IReadOnlyList<IngredientCategoryDto>>.Failure)?.Error
            ?? (await units as ApiResult<IReadOnlyList<UnitDto>>.Failure)?.Error
            ?? (await currencies as ApiResult<IReadOnlyList<CurrencyDto>>.Failure)?.Error
            ?? (await flours as ApiResult<PagedResult<FlourDto>>.Failure)?.Error
            ?? (await flourTypes as ApiResult<IReadOnlyList<FlourTypeDto>>.Failure)?.Error
            ?? ApiError.FromStatus(500);
        dispatcher.Dispatch(new CatalogReferenceLoaded(new Loadable<CatalogReference>.Failed(error)));
    }
}
