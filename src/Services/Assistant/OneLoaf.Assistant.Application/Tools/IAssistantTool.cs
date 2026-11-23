using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>State shared by the tools of one turn.</summary>
public sealed class ToolContext(Guid userId, Guid conversationId)
{
    /// <summary>Current user.</summary>
    public Guid UserId { get; } = userId;

    /// <summary>Conversation of the turn.</summary>
    public Guid ConversationId { get; } = conversationId;

    /// <summary>A batch proposal created in this turn, if any.</summary>
    public PendingActionDto? PendingAction { get; set; }
}

/// <summary>Result of a tool call.</summary>
/// <param name="Content">Compact JSON (success) or a short error message.</param>
/// <param name="IsError">The call failed (bad input, not found, Core API error).</param>
public sealed record ToolResult(string Content, bool IsError)
{
    /// <summary>A failed call.</summary>
    public static ToolResult Error(string message) => new(JsonSerializer.Serialize(new { error = message }), true);
}

/// <summary>A tool the model can call. New tools are new classes registered in DI; the registry does not change.</summary>
public interface IAssistantTool
{
    /// <summary>Definition sent to Claude.</summary>
    ClaudeToolDefinition Definition { get; }

    /// <summary>Validates the input and executes the call.</summary>
    Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken);
}
