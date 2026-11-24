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

    /// <summary>
    /// Maps a camelCase JSON path to a field of the form model: <c>displayName</c> → <c>DisplayName</c>,
    /// <c>stages[0].maxDurationMinutes</c> → <c>Stages[0].MaxDurationMinutes</c> (the request lists and the form lists
    /// keep the same order). Returns null when the path does not exist on the form.
    /// </summary>
    internal static FieldIdentifier? FieldFor(object model, string path)
    {
        var segments = path.Split('.');
        object current = model;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            int? index = null;
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            if (bracket > 0 && segment.EndsWith(']') && int.TryParse(segment[(bracket + 1)..^1], out var parsed))
            {
                index = parsed;
                segment = segment[..bracket];
            }

            var property = current.GetType().GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (property is null)
            {
                return null;
            }

            var last = i == segments.Length - 1;
            if (index is null)
            {
                if (last)
                {
                    return new FieldIdentifier(current, property.Name);
                }

                current = property.GetValue(current)!;
                if (current is null)
                {
                    return null;
                }

                continue;
            }

            if (property.GetValue(current) is not System.Collections.IList list || index >= list.Count || list[index.Value] is not { } item)
            {
                return null;
            }

            if (last)
            {
                // "ingredients[2]" without a member: the message belongs to the row; shown with the list property.
                return new FieldIdentifier(current, property.Name);
            }

            current = item;
        }

        return null;
    }

    private FieldIdentifier? FieldFor(string path) => FieldFor(_context!.Model, path);
}
