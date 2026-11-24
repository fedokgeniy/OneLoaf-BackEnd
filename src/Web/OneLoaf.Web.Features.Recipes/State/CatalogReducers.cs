using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Reference;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

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
            PhotoSrc = action.Id == state.SelectedId ? state.PhotoSrc : null,
            PhotoLoading = action.Id is { } selected && selected != state.SelectedId
                && state.List.Value?.Items.FirstOrDefault(i => i.Id == selected)?.HasPhoto == true,
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
    [ReducerMethod(typeof(AddRecord))]
    public static CatalogState OnAddRecord(CatalogState state) => state with { Busy = true };

    /// <summary>Photo of the selected ingredient.</summary>
    [ReducerMethod]
    public static CatalogState OnPhoto(CatalogState state, IngredientPhotoLoaded action) =>
        action.Id == state.SelectedId ? state with { PhotoSrc = action.Src, PhotoLoading = false, PhotoBusy = false } : state;

    /// <summary>A photo command runs.</summary>
    [ReducerMethod(typeof(UploadIngredientPhoto))]
    public static CatalogState OnUploadPhoto(CatalogState state) => state with { PhotoBusy = true };

    /// <summary>A photo command runs.</summary>
    [ReducerMethod(typeof(RemoveIngredientPhoto))]
    public static CatalogState OnRemovePhoto(CatalogState state) => state with { PhotoBusy = true };

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
    public static CatalogState OnFailed(CatalogState state) => state with { Busy = false, PhotoBusy = false };

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
