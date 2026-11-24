using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.Models;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Forms;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Pages;

/// <summary>
/// <c>/ingredients</c> (US-10–12): search and category filter, the list with today's price, and the selected
/// ingredient with its flour details, price history and the "add price" form. List and detail side by side on desktop
/// (Ingredients-Desktop), one after the other on smaller screens.
/// </summary>
public sealed partial class IngredientsPage
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private string _search = string.Empty;
    private CancellationTokenSource? _searchDelay;
    private PriceForm _priceForm = new();
    private ServerValidation? _priceServer;
    private ApiError? _priceError;
    private IngredientForm? _ingredientForm;
    private IngredientDto? _editing;
    private ServerValidation? _ingredientServer;
    private ApiError? _ingredientError;
    private IngredientDto? _confirmDelete;

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

    private void ResetPriceForm()
    {
        var prices = State.Value.Prices.Value;
        var last = prices is { Count: > 0 } ? prices[0] : null;
        _priceForm = new PriceForm
        {
            Currency = last?.Currency ?? Selected?.CurrentPrice?.Currency ?? "EUR",
            PerQuantity = last?.PerQuantity ?? 1000m,
            EffectiveFrom = Time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

    /// <summary>Field errors go under the field; a duplicate price date (409) under the date, a duplicate name under the name.</summary>
    private void OnCommandFailed(CatalogCommandFailed failed)
    {
        var error = failed.Error;
        switch (failed.Command)
        {
            case nameof(Recipes.State.AddPrice):
                if (error.Kind == ApiErrorKind.Validation && _priceServer is not null && _priceServer.Display(error.FieldErrors).Count == 0)
                {
                    return;
                }

                if (error.Kind == ApiErrorKind.Conflict && _priceServer is not null)
                {
                    _priceServer.DisplayFor(nameof(PriceForm.EffectiveFrom), error.Message);
                    return;
                }

                _priceError = error;
                break;
            case nameof(Recipes.State.SaveIngredient):
                if (error.Kind == ApiErrorKind.Validation && _ingredientServer is not null && _ingredientServer.Display(error.FieldErrors).Count == 0)
                {
                    return;
                }

                if (error.Kind == ApiErrorKind.Conflict && _ingredientServer is not null)
                {
                    _ingredientServer.DisplayFor(nameof(IngredientForm.Name), error.Message);
                    return;
                }

                _ingredientError = error;
                break;
        }

        StateHasChanged();
    }
}
