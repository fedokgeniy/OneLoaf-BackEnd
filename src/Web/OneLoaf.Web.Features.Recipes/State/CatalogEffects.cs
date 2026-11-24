using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Reference;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

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
            var photo = state.Value.PhotoLoading ? api.GetIngredientPhotoAsync(id) : null;
            dispatcher.Dispatch(new PricesLoaded(id, (await prices).ToLoadable()));
            dispatcher.Dispatch(new MovementsLoaded(id, (await movements).ToLoadable()));
            if (photo is not null)
            {
                dispatcher.Dispatch(new IngredientPhotoLoaded(id, await photo is ApiResult<FileDownload>.Success { Value: var file } ? file.ToDataUrl() : null));
            }
        }
    }

    /// <summary>Uploads the photo and shows it at once: the page already has the bytes.</summary>
    [EffectMethod]
    public async Task HandleUploadPhoto(UploadIngredientPhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.SetIngredientPhotoAsync(action.Id, action.Photo))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(UploadIngredientPhoto), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new IngredientPhotoLoaded(action.Id,
                    new FileDownload(action.Photo.Content, action.Photo.ContentType, action.Photo.FileName).ToDataUrl()));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Photo saved")));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                break;
        }
    }

    /// <summary>Removes the photo.</summary>
    [EffectMethod]
    public async Task HandleRemovePhoto(RemoveIngredientPhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeleteIngredientPhotoAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(RemoveIngredientPhoto), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new IngredientPhotoLoaded(action.Id, null));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Photo removed")));
                dispatcher.Dispatch(new LoadIngredients(state.Value.Search, state.Value.Category));
                break;
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

    /// <summary>Records what was bought; stock, ledger, price history and list come from the server again.</summary>
    [EffectMethod]
    public async Task HandleAddRecord(AddRecord action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AddRecordAsync(action.IngredientId, action.Request))
        {
            case ApiResult<IngredientDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new CatalogCommandFailed(nameof(AddRecord), error));
                break;
            default:
                AfterChange(dispatcher, new StockChanged(action.IngredientId),
                    action.Request.PriceOnly ? "Price recorded" : "Purchase recorded", action.IngredientId);
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
