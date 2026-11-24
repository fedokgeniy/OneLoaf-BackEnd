namespace OneLoaf.Assistant.Application.Chat;

/// <summary>Conversations and messages (<c>chat_conversations</c>, <c>chat_messages</c>); scoped to the owner.</summary>
public interface IConversationRepository
{
    /// <summary>Conversations, newest first.</summary>
    Task<IReadOnlyList<ConversationDto>> ListAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>The conversation, or null.</summary>
    Task<ConversationDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Creates a conversation.</summary>
    Task<ConversationDto> CreateAsync(Guid ownerId, string? title, CancellationToken cancellationToken);

    /// <summary>Deletes a conversation with its messages; false when not found.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Messages in order; tool messages only when requested.</summary>
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, bool includeTools, CancellationToken cancellationToken);

    /// <summary>Appends a message and touches the conversation; returns the stored message.</summary>
    Task<MessageDto> AddMessageAsync(Guid conversationId, NewMessage message, CancellationToken cancellationToken);

    /// <summary>Sets the title when it is empty.</summary>
    Task SetTitleIfEmptyAsync(Guid conversationId, string title, CancellationToken cancellationToken);
}
