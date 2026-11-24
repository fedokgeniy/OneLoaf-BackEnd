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
/// stock (quantity and movements). One "Add record" enters what was bought: the price and, unless it is price only,
/// the quantity; the card also holds a photo of the ingredient or its pack. List and detail side by side on desktop
/// (Ingredients-Desktop), one after the other on smaller screens. Every quantity and price comes from the Core API.
/// The page owns the state and the commands; the card and the dialogs are components that show data and raise events.
/// </summary>
public sealed partial class IngredientsPage
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private string _search = string.Empty;
    private CancellationTokenSource? _searchDelay;

    private PriceDto? _editingPrice;
    private PriceForm _priceEditForm = new();
    private ApiError? _priceEditError;
    private PriceDto? _confirmDeletePrice;

    private IngredientForm? _ingredientForm;
    private IngredientDto? _editing;
    private ApiError? _ingredientError;
    private IngredientDto? _confirmDelete;

    private bool _flourOpen;
    private FlourDto? _editingFlour;
    private ApiError? _flourError;
    private FlourDto? _confirmDeleteFlour;

    private IngredientDto? _recordFor;
    private IngredientRecordForm _recordForm = new();
    private ApiError? _recordError;

    private IngredientDto? _adjustFor;
    private StockAdjustmentForm _adjustForm = new();
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
        Reference?.Flours.Select(f => new SelectOption(f.Id.ToString(), f.ProteinPercent is { } p ? $"{f.Name} · {DisplayFormat.Percent(p)}" : f.Name)).ToList() ?? [];

    private IReadOnlyList<SelectOption> FlourTypeOptions =>
        Reference?.FlourTypes.Select(t => new SelectOption(t.Code, t.Name)).ToList() ?? [];

    private string Today => Time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _search = State.Value.Search;

        // Always from the server: starters, recipes and the assistant add ingredients in other sections.
        Reload();

        SubscribeToAction<CatalogCommandFailed>(OnCommandFailed);
        SubscribeToAction<IngredientSaved>(_ => CloseIngredientForm());
        SubscribeToAction<PriceChanged>(_ =>
        {
            _editingPrice = null;
            _confirmDeletePrice = null;
        });
        SubscribeToAction<StockChanged>(_ =>
        {
            _recordFor = null;
            _adjustFor = null;
        });
        SubscribeToAction<FlourSaved>(OnFlourSaved);
        SubscribeToAction<FlourDeleted>(_ => _confirmDeleteFlour = null);
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

    /// <summary>The record starts from the last purchase: same currency and pack size (else the quantity of the last price).</summary>
    private void OpenRecord(IngredientDto ingredient)
    {
        _recordError = null;
        var lastPurchase = State.Value.Movements.Value?.Items.FirstOrDefault(m => m.PackSize is not null);
        _recordForm = new IngredientRecordForm
        {
            Currency = ingredient.CurrentPrice?.Currency ?? "EUR",
            PackSize = lastPurchase?.PackSize ?? ingredient.CurrentPrice?.PerQuantity,
            Date = Today,
        };
        _recordFor = ingredient;
    }

    private void SaveRecord()
    {
        if (State.Value.Busy || _recordFor is null)
        {
            return;
        }

        _recordError = null;
        Dispatcher.Dispatch(new AddRecord(_recordFor.Id, _recordForm.ToRequest()));
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

    /// <summary>The form that sent the command shows its error: under the field when it has one, else as an alert.</summary>
    private void OnCommandFailed(CatalogCommandFailed failed)
    {
        var error = failed.Error;
        switch (failed.Command)
        {
            case nameof(Recipes.State.UpdatePrice):
                _priceEditError = error;
                break;
            case nameof(Recipes.State.SaveIngredient):
                _ingredientError = error;
                break;
            case nameof(Recipes.State.AddRecord):
                _recordError = error;
                break;
            case nameof(Recipes.State.AdjustStock):
                _adjustError = error;
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
}
