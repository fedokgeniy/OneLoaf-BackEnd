using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.Models;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Formatting;
using OneLoaf.Web.Shared.Forms;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Pages;

/// <summary>
/// <c>/ingredients</c> (US-10–12 and the stock): search and category filter, the list with today's price and the
/// stock, and the selected ingredient with its flour details and two tabs: prices (history, add, correct, delete) and
/// stock (quantity, purchases, corrections, movements). List and detail side by side on desktop
/// (Ingredients-Desktop), one after the other on smaller screens. Every quantity and price comes from the Core API.
/// </summary>
public sealed partial class IngredientsPage
{
    private const string PricesTab = "prices";
    private const string StockTab = "stock";

    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private static readonly IReadOnlyList<TabItem> DetailTabs = [new(PricesTab, "Prices"), new(StockTab, "Stock")];

    private string _search = string.Empty;
    private CancellationTokenSource? _searchDelay;
    private string _tab = PricesTab;

    private PriceForm _priceForm = new();
    private ServerValidation? _priceServer;
    private ApiError? _priceError;

    private PriceDto? _editingPrice;
    private PriceForm _priceEditForm = new();
    private ServerValidation? _priceEditServer;
    private ApiError? _priceEditError;
    private PriceDto? _confirmDeletePrice;

    private IngredientForm? _ingredientForm;
    private IngredientDto? _editing;
    private ServerValidation? _ingredientServer;
    private ApiError? _ingredientError;
    private IngredientDto? _confirmDelete;

    private bool _flourOpen;
    private FlourDto? _editingFlour;
    private ApiError? _flourError;
    private FlourDto? _confirmDeleteFlour;

    private IngredientDto? _purchaseFor;
    private PurchaseForm _purchaseForm = new();
    private ServerValidation? _purchaseServer;
    private ApiError? _purchaseError;

    private IngredientDto? _adjustFor;
    private StockAdjustmentForm _adjustForm = new();
    private ServerValidation? _adjustServer;
    private ApiError? _adjustError;

    [Inject]
    private IState<CatalogState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private IngredientDto? Selected => State.Value.List.Value?.Items.FirstOrDefault(i => i.Id == State.Value.SelectedId);

    private bool IsFiltered => State.Value.Search.Length > 0 || State.Value.Category is not null;

    private CatalogReference? Reference => State.Value.Reference.Value;

    private IReadOnlyList<SelectOption> CategoryOptions =>
        Reference?.Categories.Select(c => new SelectOption(c.Code, c.Name)).ToList() ?? [];

    private IReadOnlyList<SelectOption> UnitOptions =>
        Reference?.Units.Select(u => new SelectOption(u.Code, $"{u.Name} ({u.Code})")).ToList() ?? [new SelectOption("g", "gram (g)")];

    private IReadOnlyList<SelectOption> CurrencyOptions =>
        Reference?.Currencies.Select(c => new SelectOption(c.Code, c.Code)).ToList() ?? [new SelectOption("EUR", "EUR")];

    private IReadOnlyList<SelectOption> FlourOptions =>
        Reference?.Flours.Select(f => new SelectOption(f.Id.ToString(), f.ProteinPercent is { } p ? $"{f.Name} · {p:0.#} %" : f.Name)).ToList() ?? [];

    private IReadOnlyList<SelectOption> FlourTypeOptions =>
        Reference?.FlourTypes.Select(t => new SelectOption(t.Code, t.Name)).ToList() ?? [];

    private string Today => Time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _search = State.Value.Search;
        ResetPriceForm();
        if (State.Value.List is Shared.State.Loadable<Shared.Contracts.PagedResult<IngredientDto>>.NotAsked)
        {
            Reload();
        }

        SubscribeToAction<CatalogCommandFailed>(OnCommandFailed);
        SubscribeToAction<IngredientSaved>(_ => CloseIngredientForm());
        SubscribeToAction<PriceAdded>(_ => ResetPriceForm());
        SubscribeToAction<PriceChanged>(_ =>
        {
            _editingPrice = null;
            _confirmDeletePrice = null;
        });
        SubscribeToAction<StockChanged>(_ =>
        {
            _purchaseFor = null;
            _adjustFor = null;
        });
        SubscribeToAction<FlourSaved>(OnFlourSaved);
        SubscribeToAction<FlourDeleted>(_ => _confirmDeleteFlour = null);
        SubscribeToAction<SelectIngredient>(_ =>
        {
            _priceError = null;
            ResetPriceForm();
        });
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(bool disposing)
    {
        if (disposing)
        {
            _searchDelay?.Cancel();
            _searchDelay?.Dispose();
        }

        return base.DisposeAsyncCore(disposing);
    }

    private void Reload() => Dispatcher.Dispatch(new LoadIngredients(State.Value.Search, State.Value.Category));

    private async Task OnSearch(ChangeEventArgs e)
    {
        _search = e.Value as string ?? string.Empty;
        _searchDelay?.Cancel();
        _searchDelay?.Dispose();
        _searchDelay = new CancellationTokenSource();
        try
        {
            await Task.Delay(SearchDelay, Time, _searchDelay.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Dispatcher.Dispatch(new LoadIngredients(_search.Trim(), State.Value.Category));
    }

    private void SetCategory(string? code) => Dispatcher.Dispatch(new LoadIngredients(State.Value.Search, code));

    private string CategoryName(string code) => Reference?.Categories.FirstOrDefault(c => c.Code == code)?.Name ?? code;

    private bool IsFlourCategory(string? code) => Reference?.Categories.FirstOrDefault(c => c.Code == code)?.IsFlour == true;

    private FlourDto? FlourOf(IngredientDto ingredient) => Reference?.Flours.FirstOrDefault(f => f.Id == ingredient.FlourId);

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
    private static string? MovementDetail(StockMovementDto movement, IngredientDto ingredient)
    {
        if (movement is { Type: "purchase", PackCount: { } packs, PackSize: { } size, TotalCost: { } cost })
        {
            var text = $"{packs} × {DisplayFormat.Quantity(size, ingredient.UnitCode)} for {DisplayFormat.Money(cost, movement.Currency)}";
            return movement.Note is null ? text : $"{text} · {movement.Note}";
        }

        return movement.BatchName ?? movement.Note;
    }

    private static string PackHelp(IngredientDto ingredient) => ingredient.UnitCode switch
    {
        "g" => "A 1 kg bag is 1000 g.",
        "ml" => "A 1 litre bottle is 1000 ml.",
        "pcs" => "A box of ten eggs is 10 pcs.",
        _ => $"In {ingredient.UnitCode}, the unit of this ingredient.",
    };

    private void ResetPriceForm()
    {
        var prices = State.Value.Prices.Value;
        var last = prices is { Count: > 0 } ? prices[0] : null;
        _priceForm = new PriceForm
        {
            Currency = last?.Currency ?? Selected?.CurrentPrice?.Currency ?? "EUR",
            PerQuantity = last?.PerQuantity ?? 1000m,
            EffectiveFrom = Today,
        };
    }

    private void AddPrice(IngredientDto ingredient)
    {
        if (State.Value.Busy)
        {
            return;
        }

        _priceError = null;
        Dispatcher.Dispatch(new AddPrice(ingredient.Id, _priceForm.ToRequest()));
    }

    private void OpenPriceEdit(PriceDto price)
    {
        _priceEditError = null;
        _priceEditForm = PriceForm.From(price);
        _editingPrice = price;
    }

    private void SavePriceEdit()
    {
        if (State.Value.Busy || _editingPrice is null || Selected is not { } ingredient)
        {
            return;
        }

        _priceEditError = null;
        Dispatcher.Dispatch(new UpdatePrice(ingredient.Id, _editingPrice.Id, _priceEditForm.ToRequest()));
    }

    private void DeletePrice()
    {
        if (!State.Value.Busy && _confirmDeletePrice is { } price && Selected is { } ingredient)
        {
            Dispatcher.Dispatch(new DeletePrice(ingredient.Id, price.Id));
        }
    }

    private void OpenIngredientForm(IngredientDto? ingredient)
    {
        _editing = ingredient;
        _ingredientError = null;
        _ingredientForm = ingredient is null ? new IngredientForm { CategoryCode = State.Value.Category } : IngredientForm.From(ingredient);
    }

    private void CloseIngredientForm()
    {
        _ingredientForm = null;
        _editing = null;
    }

    private void SaveIngredient()
    {
        if (State.Value.Busy || _ingredientForm is null)
        {
            return;
        }

        _ingredientError = null;
        Dispatcher.Dispatch(new SaveIngredient(_editing?.Id, _ingredientForm.ToRequest(IsFlourCategory(_ingredientForm.CategoryCode))));
    }

    private void Delete()
    {
        if (_confirmDelete is { } ingredient)
        {
            Dispatcher.Dispatch(new DeleteIngredient(ingredient.Id));
        }

        _confirmDelete = null;
    }

    private void OpenFlourForm(FlourDto? flour)
    {
        _editingFlour = flour;
        _flourError = null;
        _flourOpen = true;
    }

    private void CloseFlourForm()
    {
        _flourOpen = false;
        _editingFlour = null;
    }

    private void SaveFlour(FlourRequest request)
    {
        _flourError = null;
        Dispatcher.Dispatch(new SaveFlour(_editingFlour?.Id, request));
    }

    /// <summary>A flour added from the ingredient form is selected in it at once.</summary>
    private void OnFlourSaved(FlourSaved saved)
    {
        if (saved.Created && _ingredientForm is not null)
        {
            _ingredientForm.FlourId = saved.Flour.Id.ToString();
        }

        CloseFlourForm();
    }

    private void DeleteFlour()
    {
        if (!State.Value.Busy && _confirmDeleteFlour is { } flour)
        {
            Dispatcher.Dispatch(new DeleteFlour(flour.Id));
        }
    }

    private void OpenPurchase(IngredientDto ingredient)
    {
        _purchaseError = null;
        _purchaseForm = new PurchaseForm
        {
            Currency = ingredient.CurrentPrice?.Currency ?? "EUR",
            PackSize = ingredient.CurrentPrice?.PerQuantity,
            PurchasedOn = Today,
        };
        _purchaseFor = ingredient;
    }

    private void SavePurchase()
    {
        if (State.Value.Busy || _purchaseFor is null)
        {
            return;
        }

        _purchaseError = null;
        Dispatcher.Dispatch(new AddPurchase(_purchaseFor.Id, _purchaseForm.ToRequest()));
    }

    private void OpenAdjust(IngredientDto ingredient)
    {
        _adjustError = null;
        _adjustForm = new StockAdjustmentForm { ActualQuantity = ingredient.Stock };
        _adjustFor = ingredient;
    }

    private void SaveAdjustment()
    {
        if (State.Value.Busy || _adjustFor is null)
        {
            return;
        }

        _adjustError = null;
        Dispatcher.Dispatch(new AdjustStock(_adjustFor.Id, _adjustForm.ToRequest()));
    }

    /// <summary>
    /// Field errors go under the field; a duplicate price date (409) under the date, a duplicate name under the name.
    /// Anything else is shown as an alert in the form that sent the command.
    /// </summary>
    private void OnCommandFailed(CatalogCommandFailed failed)
    {
        var error = failed.Error;
        switch (failed.Command)
        {
            case nameof(Recipes.State.AddPrice):
                _priceError = Unplaced(error, _priceServer, nameof(PriceForm.EffectiveFrom));
                break;
            case nameof(Recipes.State.UpdatePrice):
                _priceEditError = Unplaced(error, _priceEditServer, nameof(PriceForm.EffectiveFrom));
                break;
            case nameof(Recipes.State.SaveIngredient):
                _ingredientError = Unplaced(error, _ingredientServer, nameof(IngredientForm.Name));
                break;
            case nameof(Recipes.State.AddPurchase):
                _purchaseError = Unplaced(error, _purchaseServer, null);
                break;
            case nameof(Recipes.State.AdjustStock):
                _adjustError = Unplaced(error, _adjustServer, null);
                break;
            case nameof(Recipes.State.SaveFlour):
                _flourError = error;
                break;
            case nameof(Recipes.State.DeletePrice):
                _confirmDeletePrice = null;
                break;
            case nameof(Recipes.State.DeleteFlour):
                _confirmDeleteFlour = null;
                break;
        }

        StateHasChanged();
    }

    /// <summary>Puts the error under its field when it can and returns what is left to show as an alert.</summary>
    private static ApiError? Unplaced(ApiError error, ServerValidation? server, string? conflictField)
    {
        if (error.Kind == ApiErrorKind.Validation && server is not null && server.Display(error.FieldErrors).Count == 0)
        {
            return null;
        }

        if (error.Kind == ApiErrorKind.Conflict && server is not null && conflictField is not null)
        {
            server.DisplayFor(conflictField, error.Message);
            return null;
        }

        return error;
    }
}
