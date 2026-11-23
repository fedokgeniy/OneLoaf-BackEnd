using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Features.Assistant.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Assistant.State;

/// <summary>What happened to a proposal.</summary>
public enum ProposalStatus
{
    /// <summary>Waiting for Confirm or Decline.</summary>
    Pending,

    /// <summary>Saved as a batch.</summary>
    Confirmed,

    /// <summary>Declined by the user.</summary>
    Declined,

    /// <summary>Expired or already used (409).</summary>
    Expired,
}

/// <summary>A message in the chat with the extras of its reply (proposal, disclaimer, unverified numbers).</summary>
public sealed record ChatEntry(
    MessageDto Message,
    BatchProposal? Proposal = null,
    ProposalStatus Status = ProposalStatus.Pending,
    Guid? BatchId = null,
    string? Disclaimer = null,
    IReadOnlyList<string>? UnverifiedNumbers = null);

/// <summary>Assistant slice: conversations, the open chat, sending, proposals, usage.</summary>
[FeatureState(Name = "Assistant")]
public sealed record AssistantState(
    Loadable<IReadOnlyList<ConversationDto>> Conversations,
    Guid? ConversationId,
    Loadable<ImmutableList<ChatEntry>> Messages,
    string? Sending,
    ApiError? SendError,
    Guid? ActingOn,
    Loadable<UsageDto> Usage)
{
    private AssistantState()
        : this(Loadable<IReadOnlyList<ConversationDto>>.Idle, null, new Loadable<ImmutableList<ChatEntry>>.Loaded([]), null, null, null,
            Loadable<UsageDto>.Idle)
    {
    }
}

/// <summary>Intent: load the conversations and today's usage.</summary>
public sealed record LoadConversations;

/// <summary>Result: conversations.</summary>
public sealed record ConversationsLoaded(Loadable<IReadOnlyList<ConversationDto>> Conversations);

/// <summary>Result: usage.</summary>
public sealed record UsageLoaded(Loadable<UsageDto> Usage);

/// <summary>Intent: open a conversation, or a new empty chat for <see langword="null"/>.</summary>
public sealed record OpenConversation(Guid? Id);

/// <summary>Result: messages of a conversation.</summary>
public sealed record MessagesLoaded(Guid Id, Loadable<ImmutableList<ChatEntry>> Messages);

/// <summary>Intent: send a message; a new chat first creates its conversation.</summary>
public sealed record SendChatMessage(string Content);

/// <summary>Result: a new chat got its conversation (the page moves to its URL).</summary>
public sealed record ConversationStarted(ConversationDto Conversation);

/// <summary>Result: the user message and the assistant reply.</summary>
public sealed record ChatReplied(Guid ConversationId, MessageDto UserMessage, ChatReply Reply);

/// <summary>Result: sending failed; the draft stays in the composer.</summary>
public sealed record ChatSendFailed(ApiError Error);

/// <summary>Intent: confirm a proposal (rule 2: the only way a batch is saved from the chat).</summary>
public sealed record ConfirmProposal(Guid ActionId);

/// <summary>Intent: decline a proposal.</summary>
public sealed record DeclineProposal(Guid ActionId);

/// <summary>Result: a proposal changed status.</summary>
public sealed record ProposalSettled(Guid ActionId, ProposalStatus Status, Guid? BatchId);

/// <summary>Result: confirm or decline failed for another reason than expiry.</summary>
public sealed record ProposalFailed(Guid ActionId, ApiError Error);

/// <summary>Intent: delete a conversation.</summary>
public sealed record DeleteConversation(Guid Id);

/// <summary>Result: deleted.</summary>
public sealed record ConversationDeleted(Guid Id);

/// <summary>Reducers of <see cref="AssistantState"/>.</summary>
public static class AssistantReducers
{
    /// <summary>Conversations.</summary>
    [ReducerMethod]
    public static AssistantState OnConversations(AssistantState state, ConversationsLoaded action) => state with { Conversations = action.Conversations };

    /// <summary>Usage.</summary>
    [ReducerMethod]
    public static AssistantState OnUsage(AssistantState state, UsageLoaded action) => state with { Usage = action.Usage };

    /// <summary>Opening a chat; a new chat is empty at once.</summary>
    [ReducerMethod]
    public static AssistantState OnOpen(AssistantState state, OpenConversation action) =>
        action.Id == state.ConversationId && state.Messages is Loadable<ImmutableList<ChatEntry>>.Loaded
            ? state
            : state with
            {
                ConversationId = action.Id,
                Messages = action.Id is null ? new Loadable<ImmutableList<ChatEntry>>.Loaded([]) : new Loadable<ImmutableList<ChatEntry>>.Loading(null),
                SendError = null,
                Sending = null,
            };

    /// <summary>Messages of the open conversation.</summary>
    [ReducerMethod]
    public static AssistantState OnMessages(AssistantState state, MessagesLoaded action) =>
        action.Id == state.ConversationId ? state with { Messages = action.Messages } : state;

    /// <summary>Sending: the page shows the message and the typing indicator.</summary>
    [ReducerMethod]
    public static AssistantState OnSend(AssistantState state, SendChatMessage action) => state with { Sending = action.Content, SendError = null };

    /// <summary>A new chat got its id; the list gets it at the top.</summary>
    [ReducerMethod]
    public static AssistantState OnStarted(AssistantState state, ConversationStarted action) => state with
    {
        ConversationId = action.Conversation.Id,
        Conversations = state.Conversations.Value is { } list
            ? new Loadable<IReadOnlyList<ConversationDto>>.Loaded([action.Conversation, .. list])
            : state.Conversations,
    };

    /// <summary>Reply arrived.</summary>
    [ReducerMethod]
    public static AssistantState OnReplied(AssistantState state, ChatReplied action)
    {
        if (action.ConversationId != state.ConversationId)
        {
            return state with { Sending = null };
        }

        var reply = action.Reply;
        var messages = (state.Messages.Value ?? []).Add(new ChatEntry(action.UserMessage))
            .Add(new ChatEntry(reply.Message, reply.Proposal, Disclaimer: reply.Disclaimer, UnverifiedNumbers: reply.UnverifiedNumbers));
        return state with { Sending = null, Messages = new Loadable<ImmutableList<ChatEntry>>.Loaded(messages) };
    }

    /// <summary>Send failed.</summary>
    [ReducerMethod]
    public static AssistantState OnSendFailed(AssistantState state, ChatSendFailed action) => state with { Sending = null, SendError = action.Error };

    /// <summary>Confirming.</summary>
    [ReducerMethod]
    public static AssistantState OnConfirm(AssistantState state, ConfirmProposal action) => state with { ActingOn = action.ActionId };

    /// <summary>Declining.</summary>
    [ReducerMethod]
    public static AssistantState OnDecline(AssistantState state, DeclineProposal action) => state with { ActingOn = action.ActionId };

    /// <summary>Proposal settled.</summary>
    [ReducerMethod]
    public static AssistantState OnSettled(AssistantState state, ProposalSettled action) => state with
    {
        ActingOn = null,
        Messages = state.Messages.Value is { } messages
            ? new Loadable<ImmutableList<ChatEntry>>.Loaded(messages.ConvertAll(m =>
                m.Proposal?.ActionId == action.ActionId ? m with { Status = action.Status, BatchId = action.BatchId } : m))
            : state.Messages,
    };

    /// <summary>Confirm or decline failed.</summary>
    [ReducerMethod(typeof(ProposalFailed))]
    public static AssistantState OnProposalFailed(AssistantState state) => state with { ActingOn = null };

    /// <summary>Deleted.</summary>
    [ReducerMethod]
    public static AssistantState OnDeleted(AssistantState state, ConversationDeleted action) => state with
    {
        Conversations = state.Conversations.Value is { } list
            ? new Loadable<IReadOnlyList<ConversationDto>>.Loaded([.. list.Where(c => c.Id != action.Id)])
            : state.Conversations,
        ConversationId = state.ConversationId == action.Id ? null : state.ConversationId,
        Messages = state.ConversationId == action.Id ? new Loadable<ImmutableList<ChatEntry>>.Loaded([]) : state.Messages,
    };
}

/// <summary>Side effects of the assistant.</summary>
public sealed class AssistantEffects(IAssistantApi api, IState<AssistantState> state, TimeProvider time)
{
    /// <summary>Conversations and usage in parallel.</summary>
    [EffectMethod(typeof(LoadConversations))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        var usage = api.GetUsageAsync();
        dispatcher.Dispatch(new ConversationsLoaded((await api.ListConversationsAsync()).ToLoadable()));
        dispatcher.Dispatch(new UsageLoaded((await usage).ToLoadable()));
    }

    /// <summary>Messages of a conversation (tool messages are not shown).</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenConversation action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (action.Id is not { } id || state.Value.Messages is not Loadable<ImmutableList<ChatEntry>>.Loading)
        {
            return;
        }

        dispatcher.Dispatch(new MessagesLoaded(id, await api.ListMessagesAsync(id) switch
        {
            ApiResult<IReadOnlyList<MessageDto>>.Success { Value: var messages } =>
                new Loadable<ImmutableList<ChatEntry>>.Loaded([.. messages.Where(m => m.Role is "user" or "assistant").Select(m => new ChatEntry(m))]),
            ApiResult<IReadOnlyList<MessageDto>>.Failure { Error: var error } => new Loadable<ImmutableList<ChatEntry>>.Failed(error),
            _ => throw new InvalidOperationException(),
        }));
    }

    /// <summary>Creates the conversation when needed, then sends; usage is refreshed after every turn.</summary>
    [EffectMethod]
    public async Task HandleSend(SendChatMessage action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var conversationId = state.Value.ConversationId;
        if (conversationId is null)
        {
            switch (await api.CreateConversationAsync(null))
            {
                case ApiResult<Shared.Contracts.Assistant.ConversationDto>.Success { Value: var conversation }:
                    conversationId = conversation.Id;
                    dispatcher.Dispatch(new ConversationStarted(conversation));
                    break;
                case ApiResult<Shared.Contracts.Assistant.ConversationDto>.Failure { Error: var createError }:
                    dispatcher.Dispatch(new ChatSendFailed(createError));
                    return;
            }
        }

        var id = conversationId!.Value;
        switch (await api.SendAsync(id, action.Content))
        {
            case ApiResult<ChatReply>.Success { Value: var reply }:
                var user = new MessageDto(Guid.NewGuid(), "user", action.Content, null, null, time.GetUtcNow());
                dispatcher.Dispatch(new ChatReplied(id, user, reply));
                break;
            case ApiResult<ChatReply>.Failure { Error: var error }:
                dispatcher.Dispatch(new ChatSendFailed(error));
                break;
        }

        dispatcher.Dispatch(new UsageLoaded((await api.GetUsageAsync()).ToLoadable()));
    }

    /// <summary>Confirms: the batch is saved by the API, the other features reload and a toast links to it.</summary>
    [EffectMethod]
    public async Task HandleConfirm(ConfirmProposal action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.ConversationId is not { } id)
        {
            return;
        }

        switch (await api.ConfirmAsync(id, action.ActionId))
        {
            case ApiResult<ConfirmActionResponse>.Success { Value: var confirmed }:
                var name = state.Value.Messages.Value?.FirstOrDefault(m => m.Proposal?.ActionId == action.ActionId)?.Proposal?.Batch?.Name ?? "Batch";
                dispatcher.Dispatch(new ProposalSettled(action.ActionId, ProposalStatus.Confirmed, confirmed.BatchId));
                dispatcher.Dispatch(new BatchCreated(confirmed.BatchId, name));
                dispatcher.Dispatch(new ShowToast(Toast.Success($"{name} saved", "Open batch", $"batches/{confirmed.BatchId}")));
                break;
            case ApiResult<ConfirmActionResponse>.Failure { Error.Kind: ApiErrorKind.Conflict }:
                dispatcher.Dispatch(new ProposalSettled(action.ActionId, ProposalStatus.Expired, null));
                break;
            case ApiResult<ConfirmActionResponse>.Failure { Error: var error }:
                dispatcher.Dispatch(new ProposalFailed(action.ActionId, error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
        }
    }

    /// <summary>Declines.</summary>
    [EffectMethod]
    public async Task HandleDecline(DeclineProposal action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.ConversationId is not { } id)
        {
            return;
        }

        switch (await api.DeclineAsync(id, action.ActionId))
        {
            case ApiResult.Failure { Error.Kind: ApiErrorKind.Conflict }:
                dispatcher.Dispatch(new ProposalSettled(action.ActionId, ProposalStatus.Expired, null));
                break;
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new ProposalFailed(action.ActionId, error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new ProposalSettled(action.ActionId, ProposalStatus.Declined, null));
                break;
        }
    }

    /// <summary>Deletes a conversation.</summary>
    [EffectMethod]
    public async Task HandleDelete(DeleteConversation action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeleteConversationAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new ConversationDeleted(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Info("Conversation deleted")));
                break;
        }
    }
}
