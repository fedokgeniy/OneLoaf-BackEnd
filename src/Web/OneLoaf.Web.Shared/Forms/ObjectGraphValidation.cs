using System.Collections;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace OneLoaf.Web.Shared.Forms;

/// <summary>
/// DataAnnotations validation of a form model and of the items of its list properties (ingredient lines, stages,
/// batch items); Blazor's <c>DataAnnotationsValidator</c> checks top-level properties only. Messages are keyed by the
/// item, so they appear under the right row. Use it instead of <c>DataAnnotationsValidator</c>.
/// </summary>
public sealed class ObjectGraphValidation : ComponentBase, IDisposable
{
    private ValidationMessageStore? _store;
    private EditContext? _context;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_context is null)
        {
            return;
        }

        _context.OnValidationRequested -= OnValidationRequested;
        _context.OnFieldChanged -= OnFieldChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_context, CurrentEditContext))
        {
            return;
        }

        Dispose();
        _context = CurrentEditContext
            ?? throw new InvalidOperationException($"{nameof(ObjectGraphValidation)} must be placed inside an EditForm.");
        _store = new ValidationMessageStore(_context);
        _context.OnValidationRequested += OnValidationRequested;
        _context.OnFieldChanged += OnFieldChanged;
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _store!.Clear();
        foreach (var target in Targets(_context!.Model))
        {
            foreach (var result in Validate(target))
            {
                foreach (var member in result.MemberNames)
                {
                    _store.Add(new FieldIdentifier(target, member), result.ErrorMessage ?? "Invalid value.");
                }
            }
        }

        _context.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        var field = e.FieldIdentifier;
        _store!.Clear(field);
        foreach (var result in Validate(field.Model).Where(r => r.MemberNames.Contains(field.FieldName)))
        {
            _store.Add(field, result.ErrorMessage ?? "Invalid value.");
        }

        _context!.NotifyValidationStateChanged();
    }

    private static List<ValidationResult> Validate(object target)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(target, new ValidationContext(target), results, validateAllProperties: true);
        return results;
    }

    /// <summary>The model and every item of its list properties (one level, which is all the forms need).</summary>
    private static IEnumerable<object> Targets(object model)
    {
        yield return model;
        foreach (var property in model.GetType().GetProperties())
        {
            if (property.PropertyType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                continue;
            }

            if (property.GetValue(model) is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
