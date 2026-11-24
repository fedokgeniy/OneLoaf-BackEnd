using Fluxor;
using Fluxor.Blazor.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OneLoaf.Web.Features.Library.State;

namespace OneLoaf.Web.Features.Library.Pages;

/// <summary>Library recipe (US-40–42, LR-11): snapshot, reactions, copy, comments, report, unpublish for the author.</summary>
public partial class LibraryDetailPage : FluxorComponent
{
    private bool _signedIn;
    private string? _comment;
    private string? _commentError;
    private bool _copyOpen;
    private bool _reportOpen;
    private Guid? _reportComment;
    private string? _reportReason;
    private string? _reportError;
    private bool _confirmUnpublish;

    /// <summary>Publication id.</summary>
    [Parameter] public Guid Id { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Inject] private IState<PublicationState> State { get; set; } = default!;

    [Inject] private IDispatcher Dispatcher { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string LoginHref => $"login?returnUrl={Uri.EscapeDataString($"/library/{Id}")}";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        SubscribeToAction<PublicationCopied>(a => Navigation.NavigateTo($"recipes/{a.RecipeId}"));
        SubscribeToAction<PublicationCommandDone>(done =>
        {
            // A failed save keeps the page; the dialog closes and the toast says why.
            if (done.Command == nameof(CopyPublication))
            {
                _copyOpen = false;
            }
        });
        SubscribeToAction<PublicationUnpublished>(_ => Navigation.NavigateTo("library"));
        SubscribeToAction<ContentReported>(_ => _reportOpen = false);
        SubscribeToAction<CommentAdded>(_ => _comment = null);
        SubscribeToAction<PublicationCommandDone>(OnDone);
        _signedIn = AuthenticationState is not null && (await AuthenticationState).User.Identity?.IsAuthenticated == true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _confirmUnpublish = _reportOpen = false;
        Dispatcher.Dispatch(new OpenPublication(Id));
    }

    private void OnDone(PublicationCommandDone done)
    {
        if (done.Error is not { } error)
        {
            return;
        }

        var message = error.FieldErrors.Values.SelectMany(m => m).FirstOrDefault() ?? error.Message;
        switch (done.Command)
        {
            case nameof(AddComment):
                _commentError = message;
                break;
            case nameof(ReportContent):
                _reportError = message;
                break;
        }
    }

    private void React(string? reaction)
    {
        if (_signedIn && State.Value.Running is null)
        {
            Dispatcher.Dispatch(new SetReaction(reaction));
        }
    }

    private void SendComment()
    {
        _commentError = null;
        if (string.IsNullOrWhiteSpace(_comment))
        {
            _commentError = "Write a comment first.";
            return;
        }

        if (State.Value.Running is null)
        {
            Dispatcher.Dispatch(new AddComment(_comment.Trim()));
        }
    }

    private void OpenReport(Guid? commentId)
    {
        _reportComment = commentId;
        _reportReason = null;
        _reportError = null;
        _reportOpen = true;
    }

    private void SendReport()
    {
        _reportError = null;
        if (string.IsNullOrWhiteSpace(_reportReason))
        {
            _reportError = "Say what is wrong.";
            return;
        }

        if (State.Value.Running is null)
        {
            Dispatcher.Dispatch(new ReportContent(_reportReason.Trim(), _reportComment));
        }
    }

    private static string ReactionText(string code) => code switch
    {
        "like" => "Like",
        "love" => "Love",
        "want_to_bake" => "Want to bake",
        _ => code,
    };

    /// <summary>Opens the dialog that matches the ingredients of the recipe to the own ones.</summary>
    private void OpenCopy()
    {
        _copyOpen = true;
        Dispatcher.Dispatch(new PrepareCopy());
    }
}
