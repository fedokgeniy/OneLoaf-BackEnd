using System.Text.Json;

namespace OneLoaf.Web.Shared.Contracts.Assistant;

/// <summary>Analysis of a bake by the assistant (<c>POST /bake-records/{id}/analysis</c>, US-36).</summary>
public sealed record AnalysisDto(Guid BakeRecordId, string Analysis, IReadOnlyList<string> UnverifiedNumbers);

/// <summary>Today's assistant usage and limits (<c>GET /usage/me</c>).</summary>
public sealed record UsageDto(DateOnly Day, int Requests, int RequestLimit, long Tokens, int TokenLimit, decimal CostUsd);

/// <summary>A conversation (US-34).</summary>
public sealed record ConversationDto(Guid Id, string? Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary><c>POST /conversations</c> body.</summary>
public sealed record CreateConversationRequest(string? Title);

/// <summary>
/// A chat message. <see cref="Role"/>: user, assistant or tool. <see cref="ToolPayload"/> is only present for tool
/// messages with <c>includeTools=true</c>; the client never reads it.
/// </summary>
public sealed record MessageDto(Guid Id, string Role, string Content, string? ToolName, JsonElement? ToolPayload, DateTimeOffset CreatedAt);

/// <summary><c>POST /conversations/{id}/messages</c> body.</summary>
public sealed record SendMessageRequest(string Content);

/// <summary>
/// Reply to a message (US-34, US-35, US-38): the assistant message, a proposal waiting for confirmation, the tools it
/// used, a disclaimer and numbers it could not check against the calculator.
/// </summary>
public sealed record ChatReplyDto(
    MessageDto Message,
    PendingActionDto? PendingAction,
    IReadOnlyList<ToolCallDto> ToolCalls,
    string? Disclaimer,
    IReadOnlyList<string> UnverifiedNumbers,
    TurnUsageDto Usage);

/// <summary>
/// A proposal the user must confirm (rule 2). <see cref="Kind"/> <c>create_batch</c> carries a batch in
/// <see cref="Plan"/> — the only <see cref="JsonElement"/> in the client, mapped to a batch at once by the assistant
/// feature's API service.
/// </summary>
public sealed record PendingActionDto(Guid ActionId, string Kind, DateTimeOffset ExpiresAt, JsonElement Plan);

/// <summary>A tool the assistant called.</summary>
public sealed record ToolCallDto(string Name, bool Succeeded);

/// <summary>Tokens and cost of one turn.</summary>
public sealed record TurnUsageDto(int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary><c>POST …/actions/{actionId}/confirm</c> response: the saved batch.</summary>
public sealed record ConfirmActionResponse(Guid? BatchId, Guid? RecipeId = null);

/// <summary>
/// A recipe the assistant proposes (plan of a <c>create_recipe</c> action). The quantities are the assistant's
/// suggestion; <see cref="RecipeProposalIngredient.IsNew"/> marks ingredients that will be added without a price.
/// </summary>
public sealed record RecipeProposalDto(
    string Title,
    string? Description,
    int YieldLoaves,
    decimal? LoafWeightG,
    IReadOnlyList<RecipeProposalIngredient> Ingredients,
    IReadOnlyList<RecipeProposalStage> Stages);

/// <summary>Ingredient line of a proposed recipe; the quantity is in <see cref="UnitCode"/>.</summary>
public sealed record RecipeProposalIngredient(string Name, string CategoryCode, string UnitCode, decimal Quantity, bool IsOptional, bool IsNew);

/// <summary>Stage of a proposed recipe.</summary>
public sealed record RecipeProposalStage(string Name, string StageTypeCode, int MinDurationMinutes, int MaxDurationMinutes);
