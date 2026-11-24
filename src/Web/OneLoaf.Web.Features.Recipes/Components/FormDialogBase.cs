using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Forms;

namespace OneLoaf.Web.Features.Recipes.Components;

/// <summary>
/// Common part of the form dialogs of the ingredient screen. A dialog only shows a form the page owns and raises
/// events; the page sends the command and passes the outcome back with <see cref="Busy"/> and <see cref="Error"/>.
/// A field error goes under its field, a conflict under <see cref="ConflictField"/>, anything else into an alert.
/// </summary>
public abstract class FormDialogBase : ComponentBase
{
    private ApiError? _shown;

    /// <summary>True while the page sends the command.</summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>Error of the last command; null clears the alert.</summary>
    [Parameter] public ApiError? Error { get; set; }

    /// <summary>Raised when the form is valid and submitted.</summary>
    [Parameter] public EventCallback OnSubmit { get; set; }

    /// <summary>Raised when the dialog closes.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>The validation component of the form (set with <c>@ref</c>).</summary>
    protected ServerValidation? Server { get; set; }

    /// <summary>The part of the error that has no field: shown as an alert in the form.</summary>
    protected ApiError? Alert { get; private set; }

    /// <summary>Field a 409 belongs to (a duplicate name or date); null shows it as an alert.</summary>
    protected virtual string? ConflictField => null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Error, _shown))
        {
            return;
        }

        _shown = Error;
        Alert = Error is null ? null : Unplaced(Error);
    }

    /// <summary>Raises <see cref="OnSubmit"/> unless a command is running.</summary>
    protected Task SubmitAsync() => Busy ? Task.CompletedTask : OnSubmit.InvokeAsync();

    private ApiError? Unplaced(ApiError error)
    {
        if (error.Kind == ApiErrorKind.Validation && Server is not null && Server.Display(error.FieldErrors).Count == 0)
        {
            return null;
        }

        if (error.Kind == ApiErrorKind.Conflict && Server is not null && ConflictField is not null)
        {
            Server.DisplayFor(ConflictField, error.Message);
            return null;
        }

        return error;
    }
}
