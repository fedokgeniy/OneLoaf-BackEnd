using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Formatting;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Pages;

/// <summary>
/// <c>/recipes/{id}</c> (US-06, US-11, US-14–18, US-47–49): the recipe with the calculator. Phones and tablets show one
/// section per tab (Calculator-Phone, Calculator-Tablet); desktops show recipe and calculator side by side
/// (Calculator-Desktop). Every figure is an API response.
/// </summary>
public sealed partial class RecipeDetailPage
{
    private static readonly IReadOnlyList<TabItem> Tabs =
    [
        new("ingredients", "Ingredients"),
        new("stages", "Stages"),
        new("cost", "Cost"),
        new("timeline", "Timeline"),
    ];

    private string _tab = "cost";

    [Inject]
    private IState<RecipeDetailState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Recipe id from the route.</summary>
    [Parameter]
    public Guid Id { get; set; }

    private ProductionFiguresDto? Figures => State.Value.Figures.Value;

    private TimelineDto? Timeline => State.Value.Timeline.Value;

    private bool IsCalculating =>
        State.Value.Cost is Loadable<CostBreakdownDto>.Loading || State.Value.Figures is Loadable<ProductionFiguresDto>.Loading;

    private string? TotalText => State.Value.Cost.Value switch
    {
        { Total: { } total, Currency: var currency } => DisplayFormat.Money(total, currency),
        { } => "incomplete",
        null => null,
    };

    private string? KneadingText => Figures switch
    {
        { KneadingCycles: 1 } => "1 cycle",
        { KneadingCycles: { } cycles } => $"{cycles} cycles",
        { } => "—",
        null => null,
    };

    private string? ReadyText => Timeline is { } t ? DisplayFormat.TimeWindow(t.ReadyFrom, t.ReadyTo, t.Start) : null;

    private string AsOfValue => State.Value.AsOf?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private string StartValue =>
        State.Value.TimelineStart?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (State.Value.RecipeId != Id || State.Value.Recipe is Loadable<RecipeDto>.NotAsked)
        {
            Dispatcher.Dispatch(new OpenRecipe(Id, NextFullHour()));
        }
    }

    private static IEnumerable<RecipeIngredientDto> Flours(RecipeDto recipe) =>
        recipe.Ingredients.Where(i => i.ProteinPercent is not null);

    private static string MissingText(CostBreakdownDto cost) => cost.MissingPrices.Count switch
    {
        0 => "Cost incomplete",
        1 => "1 price missing",
        var n => $"{n} prices missing",
    };

    private static string? ErrorOf<T>(Loadable<T> value) =>
        value is Loadable<T>.Failed { Error: var error } ? ShortError(error) : null;

    private static string ShortError(ApiError error) => error.Kind switch
    {
        ApiErrorKind.BusinessRule => error.Message,
        ApiErrorKind.Network => "offline",
        _ => "not available",
    };

    /// <summary>Panels outside the selected tab are hidden on phones and tablets only (CSS shows all on desktop).</summary>
    private string PanelClass(string tab) => tab == _tab ? "recipe__panel" : "recipe__panel recipe__panel--inactive";

    private void Reload() => Dispatcher.Dispatch(new OpenRecipe(Id, State.Value.TimelineStart ?? NextFullHour()));

    private void OnAsOfChanged(ChangeEventArgs e) =>
        Dispatcher.Dispatch(new ChangeAsOf(
            DateOnly.TryParseExact(e.Value as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : null));

    private void OnStartChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParseExact(e.Value as string, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            Dispatcher.Dispatch(new ChangeTimelineStart(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local))));
        }
    }

    /// <summary>Default start of the timeline: the next full hour in the user's time zone.</summary>
    private DateTimeOffset NextFullHour()
    {
        var now = Time.GetLocalNow();
        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        return hour.AddHours(1);
    }
}
