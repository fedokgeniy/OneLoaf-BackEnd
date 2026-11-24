using System.Text.Json;

namespace OneLoaf.Assistant.Application.Claude;

/// <summary>Author of a message sent to Claude.</summary>
public enum ClaudeRole
{
    /// <summary>The baker (also carries tool results).</summary>
    User,

    /// <summary>The model.</summary>
    Assistant,
}

/// <summary>A content block of a message.</summary>
public abstract record ClaudeBlock;

/// <summary>Plain text.</summary>
/// <param name="Text">Text.</param>
public sealed record ClaudeTextBlock(string Text) : ClaudeBlock;

/// <summary>A tool call requested by the model.</summary>
/// <param name="Id">Tool use id, echoed in the result.</param>
/// <param name="Name">Tool name.</param>
/// <param name="Input">Arguments as JSON.</param>
public sealed record ClaudeToolUseBlock(string Id, string Name, JsonElement Input) : ClaudeBlock;

/// <summary>The result of a tool call, sent back in a user message.</summary>
/// <param name="ToolUseId">Id of the tool use.</param>
/// <param name="Content">Result (compact JSON or an error message).</param>
/// <param name="IsError">The tool failed.</param>
public sealed record ClaudeToolResultBlock(string ToolUseId, string Content, bool IsError) : ClaudeBlock;

/// <summary>A message of the conversation sent to Claude.</summary>
/// <param name="Role">Author.</param>
/// <param name="Blocks">Content.</param>
public sealed record ClaudeMessage(ClaudeRole Role, IReadOnlyList<ClaudeBlock> Blocks)
{
    /// <summary>A text-only message.</summary>
    public static ClaudeMessage Text(ClaudeRole role, string text) => new(role, [new ClaudeTextBlock(text)]);
}

/// <summary>A tool the model may call.</summary>
/// <param name="Name">Name (one of the tool name constants).</param>
/// <param name="Description">What the tool does and when to use it.</param>
/// <param name="InputSchema">JSON schema of the arguments (an object schema).</param>
public sealed record ClaudeToolDefinition(string Name, string Description, JsonElement InputSchema);

/// <summary>One request to Claude.</summary>
/// <param name="System">Stable system prompt (cached).</param>
/// <param name="Context">Per-request context (date, time zone), sent after the cached prefix.</param>
/// <param name="Tools">Tools, in a stable order (cached with the system prompt).</param>
/// <param name="Messages">Conversation.</param>
public sealed record ClaudeRequest(
    string System, string Context, IReadOnlyList<ClaudeToolDefinition> Tools, IReadOnlyList<ClaudeMessage> Messages);

/// <summary>Claude's reply.</summary>
/// <param name="StopReason">Why the model stopped (end_turn, tool_use, max_tokens, refusal, ...).</param>
/// <param name="Content">Text and tool use blocks.</param>
/// <param name="InputTokens">Input tokens billed.</param>
/// <param name="OutputTokens">Output tokens billed.</param>
/// <param name="CacheReadTokens">Input tokens read from the prompt cache.</param>
public sealed record ClaudeResponse(
    string StopReason, IReadOnlyList<ClaudeBlock> Content, int InputTokens, int OutputTokens, int CacheReadTokens)
{
    /// <summary>Tool calls of the reply.</summary>
    public IReadOnlyList<ClaudeToolUseBlock> ToolUses => Content.OfType<ClaudeToolUseBlock>().ToList();

    /// <summary>Text of the reply.</summary>
    public string Text => string.Join("\n", Content.OfType<ClaudeTextBlock>().Select(t => t.Text)).Trim();
}

/// <summary>Values of <see cref="ClaudeResponse.StopReason"/> the loop handles.</summary>
public static class StopReasons
{
    /// <summary>The model wants tool results.</summary>
    public const string ToolUse = "tool_use";

    /// <summary>The model finished.</summary>
    public const string EndTurn = "end_turn";

    /// <summary>The safety system declined the request.</summary>
    public const string Refusal = "refusal";
}
