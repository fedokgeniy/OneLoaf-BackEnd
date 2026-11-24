using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Formatting;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Components;

/// <summary>Parameters, events and the tab state of the ingredient card.</summary>
public sealed partial class IngredientCard
{
    private const string PricesTab = "prices";
    private const string StockTab = "stock";

    private static readonly IReadOnlyList<TabItem> DetailTabs = [new(PricesTab, "Prices"), new(StockTab, "Stock")];

    private string _tab = PricesTab;

    /// <summary>The ingredient.</summary>
    [Parameter, EditorRequired] public IngredientDto Ingredient { get; set; } = default!;

    /// <summary>Name of its category.</summary>
    [Parameter, EditorRequired] public string CategoryName { get; set; } = string.Empty;

    /// <summary>Its flour of the catalog, when it has flour details.</summary>
    [Parameter] public FlourDto? Flour { get; set; }

    /// <summary>Price history.</summary>
    [Parameter, EditorRequired] public Loadable<IReadOnlyList<PriceDto>> Prices { get; set; } = default!;

    /// <summary>Latest stock movements.</summary>
    [Parameter, EditorRequired] public Loadable<PagedResult<StockMovementDto>> Movements { get; set; } = default!;

    /// <summary>Photo as a data URL; null without a photo.</summary>
    [Parameter] public string? PhotoSrc { get; set; }

    /// <summary>True while the photo is loaded.</summary>
    [Parameter] public bool PhotoLoading { get; set; }

    /// <summary>True while the photo is uploaded or removed.</summary>
    [Parameter] public bool PhotoBusy { get; set; }

    /// <summary>Edit the ingredient.</summary>
    [Parameter] public EventCallback OnEdit { get; set; }

    /// <summary>Delete the ingredient.</summary>
    [Parameter] public EventCallback OnDelete { get; set; }

    /// <summary>Edit the flour details.</summary>
    [Parameter] public EventCallback<FlourDto> OnEditFlour { get; set; }

    /// <summary>Delete the flour details.</summary>
    [Parameter] public EventCallback<FlourDto> OnDeleteFlour { get; set; }

    /// <summary>Add a record of what was bought.</summary>
    [Parameter] public EventCallback OnAddRecord { get; set; }

    /// <summary>Correct the stock.</summary>
    [Parameter] public EventCallback OnAdjust { get; set; }

    /// <summary>Edit a price of the history.</summary>
    [Parameter] public EventCallback<PriceDto> OnEditPrice { get; set; }

    /// <summary>Delete a price of the history.</summary>
    [Parameter] public EventCallback<PriceDto> OnDeletePrice { get; set; }

    /// <summary>A photo was chosen.</summary>
    [Parameter] public EventCallback<PhotoFile> OnPhotoSelected { get; set; }

    /// <summary>Remove the photo.</summary>
    [Parameter] public EventCallback OnPhotoRemove { get; set; }

    private string PanelClass(string tab) => _tab == tab ? "ingredients__panel" : "ingredients__panel ingredients__panel--hidden";

    private static string MovementTitle(StockMovementDto movement) => movement.Type switch
    {
        "purchase" => "Purchase",
        "batch_consumption" => "Used in a batch",
        "batch_return" => "Returned from a batch",
        "starter_feeding" => "Starter feeding",
        "adjustment" => "Correction",
        _ => movement.Type,
    };

    /// <summary>Second line of a movement: the packs and the amount of a purchase, the batch, or the note.</summary>
    private string? MovementDetail(StockMovementDto movement)
    {
        if (movement is { Type: "purchase", PackCount: { } packs, PackSize: { } size, TotalCost: { } cost })
        {
            var text = $"{packs} × {DisplayFormat.Quantity(size, Ingredient.UnitCode)} for {DisplayFormat.Money(cost, movement.Currency)}";
            return movement.Note is null ? text : $"{text} · {movement.Note}";
        }

        return movement.BatchName ?? movement.Note;
    }
}
