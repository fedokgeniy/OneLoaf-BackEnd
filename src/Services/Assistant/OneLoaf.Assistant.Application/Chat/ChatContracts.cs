using System.Text.Json;
using OneLoaf.Domain;

namespace OneLoaf.Assistant.Application.Chat;

/// <summary>A conversation in the list.</summary>
/// <param name="Id">Id.</param>
/// <param name="Title">Title (first words of the first question).</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last activity.</param>
public sealed record ConversationDto(Guid Id, string? Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>A message of a conversation.</summary>
/// <param name="Id">Id.</param>
/// <param name="Role">user, assistant or tool.</param>
/// <param name="Content">Text (for a tool: a short summary).</param>
/// <param name="ToolName">Tool of a tool message.</param>
/// <param name="ToolPayload">Tool input and output (audit view only).</param>
/// <param name="CreatedAt">Time.</param>
public sealed record MessageDto(Guid Id, ChatRole Role, string Content, string? ToolName, JsonElement? ToolPayload, DateTimeOffset CreatedAt);

/// <summary>Body of <c>POST /conversations</c>.</summary>
/// <param name="Title">Optional title.</param>
public sealed record CreateConversationRequest(string? Title = null);

/// <summary>Body of <c>POST /conversations/{id}/messages</c>.</summary>
/// <param name="Content">The baker's message, up to 2000 characters.</param>
public sealed record SendMessageRequest(string Content);

/// <summary>Query of <c>GET /conversations/{id}/messages</c>.</summary>
/// <param name="IncludeTools">Include tool messages (audit).</param>
public sealed record MessageListQuery(bool IncludeTools = false);

/// <summary>A batch proposal waiting for the baker's confirmation (US-34).</summary>
/// <param name="ActionId">Id to confirm or decline.</param>
/// <param name="Kind">Kind of action (create_batch).</param>
/// <param name="ExpiresAt">After this time the proposal cannot be confirmed.</param>
/// <param name="Plan">The plan computed by the Core API (as returned by the preview).</param>
public sealed record PendingActionDto(Guid ActionId, string Kind, DateTimeOffset ExpiresAt, JsonElement Plan);

/// <summary>A tool called in the turn.</summary>
/// <param name="Name">Tool name.</param>
/// <param name="Succeeded">The call succeeded.</param>
public sealed record ToolCallDto(string Name, bool Succeeded);

/// <summary>Tokens and cost of a turn.</summary>
/// <param name="InputTokens">Input tokens.</param>
/// <param name="OutputTokens">Output tokens.</param>
/// <param name="CostUsd">Cost in USD by the configured prices.</param>
public sealed record TurnUsageDto(int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary>Reply of the assistant to one message (US-34 - US-38).</summary>
/// <param name="Message">The stored assistant message.</param>
/// <param name="PendingAction">A batch proposal to confirm, if one was made.</param>
/// <param name="ToolCalls">Tools called in the turn.</param>
/// <param name="Disclaimer">Shown for general advice not based on the baker's data.</param>
/// <param name="UnverifiedNumbers">Numbers removed because no tool produced them (should be empty).</param>
/// <param name="Usage">Tokens and cost.</param>
public sealed record ChatReplyDto(
    MessageDto Message, PendingActionDto? PendingAction, IReadOnlyList<ToolCallDto> ToolCalls, string? Disclaimer,
    IReadOnlyList<string> UnverifiedNumbers, TurnUsageDto Usage);

/// <summary>Result of confirming a proposal.</summary>
/// <param name="BatchId">The saved batch.</param>
public sealed record ConfirmActionResponse(Guid BatchId);

/// <summary>Kinds of pending actions.</summary>
public static class PendingActionKinds
{
    /// <summary>Create the proposed batch.</summary>
    public const string CreateBatch = "create_batch";
}

/// <summary>A stored proposal.</summary>
/// <param name="Id">Id.</param>
/// <param name="UserId">Owner.</param>
/// <param name="ConversationId">Conversation.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Payload">Core API request body to send on confirmation.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="ExpiresAt">Expiry.</param>
public sealed record PendingAction(
    Guid Id, Guid UserId, Guid ConversationId, string Kind, string Payload, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>A message to store.</summary>
/// <param name="Role">Role.</param>
/// <param name="Content">Text.</param>
/// <param name="ToolName">Tool name (tool messages).</param>
/// <param name="ToolPayload">Tool input/output JSON object (tool messages).</param>
/// <param name="InputTokens">Input tokens of the turn (assistant messages).</param>
/// <param name="OutputTokens">Output tokens of the turn (assistant messages).</param>
public sealed record NewMessage(
    ChatRole Role, string Content, string? ToolName = null, string? ToolPayload = null, int? InputTokens = null, int? OutputTokens = null);
