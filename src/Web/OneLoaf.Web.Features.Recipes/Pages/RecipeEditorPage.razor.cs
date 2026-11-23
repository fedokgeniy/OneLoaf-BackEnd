using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.Models;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Forms;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Pages;

/// <summary>
/// <c>/recipes/new</c>, <c>/recipes/{id}/edit</c> (US-05, US-09, US-10): general data, ingredient lines and stages as
/// editable lists with Move up / Move down / Remove; client validation of every row, server errors mapped to the row
/// field (<c>stages[0].maxDurationMinutes</c>). Archive, restore and delete for an existing recipe.
/// </summary>
public sealed partial class RecipeEditorPage
{
    private RecipeForm? _form;
    private Guid? _formFor;
    private bool _formReady;
    private ServerValidation? _server;
    private string? _summary;
    private string? _summaryReference;
    private bool _confirmDelete;
    private IReadOnlyList<SelectOption> _ingredientOptions = [];
    private IReadOnlyList<SelectOption> _stageTypeOptions = [];

    [Inject]
    private IState<RecipeEditorState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    /// <summary>Recipe id; null for a new recipe.</summary>
    [Parameter]
    public Guid? Id { get; set; }

    private string PageTitle => Id is null
        ? "New recipe"
        : State.Value.Data.Value?.Recipe is { } recipe ? $"Edit recipe · {recipe.Title}" : "Edit recipe";

    private string BackHref => Id is { } id ? $"recipes/{id}" : "recipes";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToAction<RecipeSaveFailed>(a => ShowServerError(a.Error));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!_formReady || _formFor != Id)
        {
            _form = null;
            _formReady = false;
            _formFor = Id;
            Dispatcher.Dispatch(new OpenEditor(Id));
        }
    }

    /// <summary>Builds the form once, when the data for this route has arrived.</summary>
    protected override void OnAfterRender(bool firstRender)
    {
        if (_formReady || State.Value.RecipeId != Id || State.Value.Data is not Loadable<EditorData>.Loaded { Data: var data })
        {
            return;
        }

        _form = data.Recipe is { } recipe ? RecipeForm.From(recipe) : RecipeForm.New();
        // Lines whose ingredient is not on the loaded catalog page (e.g. beyond the first 100) stay selectable.
        var missing = (data.Recipe?.Ingredients ?? [])
            .Where(line => data.Ingredients.All(i => i.Id != line.IngredientId))
            .DistinctBy(line => line.IngredientId)
            .Select(line => new SelectOption(line.IngredientId.ToString(), $"{line.IngredientName} ({line.UnitCode})"));
        _ingredientOptions = [.. data.Ingredients.Select(i => new SelectOption(i.Id.ToString(), $"{i.Name} ({i.UnitCode})"))
            .Concat(missing).OrderBy(o => o.Text, StringComparer.CurrentCultureIgnoreCase)];
        _stageTypeOptions = [.. data.StageTypes.Select(t => new SelectOption(t.Code, t.Name))];
        _formReady = true;
        StateHasChanged();
    }

    private static void Move<T>(List<T> list, int index, int offset)
    {
        var target = index + offset;
        if (target < 0 || target >= list.Count)
        {
            return;
        }

        (list[index], list[target]) = (list[target], list[index]);
    }

    private void Save()
    {
        if (State.Value.Busy || _form is null)
        {
            return;
        }

        _summary = null;
        _summaryReference = null;
        Dispatcher.Dispatch(new SaveRecipe(Id, _form.ToRequest()));
    }

    private void ShowSummary() =>
        _summary = "Check the highlighted fields; nothing was saved yet.";

    private void ShowServerError(ApiError error)
    {
        _summaryReference = error.CorrelationId;
        if (error.Kind == ApiErrorKind.Validation && _server is not null)
        {
            var unmatched = _server.Display(error.FieldErrors);
            _summary = unmatched.Count > 0 ? string.Join(" ", unmatched) : "Check the highlighted fields.";
        }
        else
        {
            _summary = error.Message;
        }

        StateHasChanged();
    }

    private void Delete()
    {
        if (Id is { } id)
        {
            Dispatcher.Dispatch(new DeleteRecipe(id));
        }

        _confirmDelete = false;
    }
}
