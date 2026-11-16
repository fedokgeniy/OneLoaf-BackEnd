namespace OneLoaf.Domain.Entities;

/// <summary>E8. Chat with the AI assistant (AI Assistant service).</summary>
public class ChatConversation
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>E8, US-34 - US-36. Message of the user, the assistant or a tool result.</summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>Name of the backend tool for tool calls and results (e.g. "calculate_cost").</summary>
    public string? ToolName { get; set; }
    /// <summary>JSON with tool input or output; numbers shown to the user come from here (US-35).</summary>
    public string? ToolPayload { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChatConversation Conversation { get; set; } = null!;
}
