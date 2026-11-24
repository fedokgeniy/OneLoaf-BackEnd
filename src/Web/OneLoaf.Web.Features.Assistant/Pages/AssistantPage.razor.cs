using Fluxor;
using Fluxor.Blazor.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using OneLoaf.Web.Features.Assistant.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Assistant.Pages;

/// <summary>Assistant chat (US-34, US-35, US-37, US-38): conversations, messages, plan cards with Confirm / Decline.</summary>
public partial class AssistantPage : FluxorComponent
{
    /// <summary>Longest message the Assistant accepts.</summary>
    public const int MaxLength = 4000;

    private string? _draft;
    private bool _listOpen;
    private ElementReference _end;
    private int _renderedCount = -1;
    private IJSObjectReference? _module;

    /// <summary>Open conversation; none for a new chat.</summary>
    [Parameter] public Guid? ConversationId { get; set; }

    [Inject] private IState<AssistantState> State { get; set; } = default!;

    [Inject] private IDispatcher Dispatcher { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToAction<ConversationStarted>(a => Navigation.NavigateTo($"assistant/{a.Conversation.Id}", replace: true));
        SubscribeToAction<ChatReplied>(_ => _draft = null);
        SubscribeToAction<ConversationDeleted>(a =>
        {
            if (a.Id == ConversationId)
            {
                Navigation.NavigateTo("assistant");
            }
        });
        if (State.Value.Conversations is Loadable<IReadOnlyList<ConversationDto>>.NotAsked)
        {
            Dispatcher.Dispatch(new LoadConversations());
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _listOpen = false;
        if (State.Value.ConversationId != ConversationId || State.Value.Messages is Loadable<System.Collections.Immutable.ImmutableList<ChatEntry>>.Failed)
        {
            Dispatcher.Dispatch(new OpenConversation(ConversationId));
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        var count = (State.Value.Messages.Value?.Count ?? 0) + (State.Value.Sending is null ? 0 : 1) + (State.Value.SendError is null ? 0 : 1);
        if (count != _renderedCount && count > 0)
        {
            _renderedCount = count;
            try
            {
                _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/OneLoaf.Web.Features.Assistant/assistant.js");
                await _module.InvokeVoidAsync("scrollToEnd", _end);
            }
            catch (JSException)
            {
                // Scrolling is a convenience; the chat stays usable without it.
            }
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(bool disposing)
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is gone with the circuit.
            }
        }

        await base.DisposeAsyncCore(disposing);
    }

    private void Delete(Guid id) => Dispatcher.Dispatch(new DeleteConversation(id));

    private void Send()
    {
        var content = _draft?.Trim();
        if (string.IsNullOrEmpty(content) || State.Value.Sending is not null)
        {
            return;
        }

        Dispatcher.Dispatch(new SendChatMessage(content));
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && (e.CtrlKey || e.MetaKey))
        {
            Send();
        }
    }

    private static string SendErrorTitle(ApiError error) => error.Kind switch
    {
        ApiErrorKind.Unavailable => "The assistant is unavailable",
        ApiErrorKind.RateLimited => "Daily assistant limit reached",
        _ => "The message was not sent",
    };
}
