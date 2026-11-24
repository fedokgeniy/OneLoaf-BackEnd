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
    /// <summary>Photo of the selected ingredient as a data URL; null without a photo or while it loads.</summary>
    public string? PhotoSrc { get; init; }

    /// <summary>The photo of the selected ingredient is being loaded.</summary>
    public bool PhotoLoading { get; init; }

    /// <summary>A photo upload or removal runs.</summary>
    public bool PhotoBusy { get; init; }

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

/// <summary>Intent: record what was bought: the price and, unless price only, the quantity.</summary>
public sealed record AddRecord(Guid IngredientId, IngredientRecordRequest Request);

/// <summary>Result: the photo of an ingredient (null = none).</summary>
public sealed record IngredientPhotoLoaded(Guid Id, string? Src);

/// <summary>Intent: upload the photo of an ingredient or its pack.</summary>
public sealed record UploadIngredientPhoto(Guid Id, OneLoaf.Web.Shared.Ui.PhotoFile Photo);

/// <summary>Intent: remove the photo of an ingredient.</summary>
public sealed record RemoveIngredientPhoto(Guid Id);

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
