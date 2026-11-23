using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace OneLoaf.Web.Shared.Forms;

/// <summary>
/// Puts the <c>errors</c> of a validation ProblemDetails (camelCase field paths) into the form's
/// <see cref="ValidationMessageStore"/>, so they appear under the matching fields. Messages are cleared when the
/// field is edited or the form is submitted again. Place it inside an <c>EditForm</c> and keep a reference.
/// </summary>
public sealed class ServerValidation : ComponentBase, IDisposable
{
    private ValidationMessageStore? _store;
    private EditContext? _context;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>Shows the server errors. Keys that match no property of the model are returned for a summary.</summary>
    /// <param name="errors">Field path → messages, as in <c>ApiError.FieldErrors</c>.</param>
    /// <returns>Messages whose field is not on the form.</returns>
    public IReadOnlyList<string> Display(IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (_context is null || _store is null)
        {
            throw new InvalidOperationException($"{nameof(ServerValidation)} must be placed inside an EditForm.");
        }

        _store.Clear();
        var unmatched = new List<string>();
        foreach (var (path, messages) in errors)
        {
            if (FieldFor(path) is { } field)
            {
                _store.Add(field, messages);
            }
            else
            {
                unmatched.AddRange(messages);
            }
        }

        _context.NotifyValidationStateChanged();
        return unmatched;
    }

    /// <summary>Shows one message under one field (e.g. a 409 "e-mail already registered").</summary>
    public void DisplayFor(string path, string message) =>
        Display(new Dictionary<string, string[]> { [path] = [message] });

    /// <summary>Removes all server messages.</summary>
    public void Clear()
    {
        _store?.Clear();
        _context?.NotifyValidationStateChanged();
    }

    /// <inheritdoc />
    public void Dispose() => Detach();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(CurrentEditContext, _context))
        {
            return;
        }

        Detach();
        _context = CurrentEditContext
            ?? throw new InvalidOperationException($"{nameof(ServerValidation)} must be placed inside an EditForm.");
        _store = new ValidationMessageStore(_context);
        _context.OnFieldChanged += OnFieldChanged;
        _context.OnValidationRequested += OnValidationRequested;
    }

    private void Detach()
    {
        if (_context is null)
        {
            return;
        }

        _context.OnFieldChanged -= OnFieldChanged;
        _context.OnValidationRequested -= OnValidationRequested;
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e) => _store?.Clear(e.FieldIdentifier);

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e) => _store?.Clear();

    /// <summary>Maps a top-level camelCase path (<c>displayName</c>) to the model property (<c>DisplayName</c>).</summary>
    private FieldIdentifier? FieldFor(string path)
    {
        var model = _context!.Model;
        var property = model.GetType().GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, path, StringComparison.OrdinalIgnoreCase));
        return property is null ? null : new FieldIdentifier(model, property.Name);
    }
}
