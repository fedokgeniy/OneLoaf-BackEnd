using System.Text.Json;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Assistant.Services;

/// <summary>A proposal of the assistant waiting for Confirm (rule 2); <see cref="Batch"/> is the plan mapped from JSON.</summary>
public sealed record BatchProposal(Guid ActionId, string Kind, DateTimeOffset ExpiresAt, BatchDto? Batch);

/// <summary>Reply of the assistant with the proposal already mapped.</summary>
public sealed record ChatReply(MessageDto Message, BatchProposal? Proposal, string? Disclaimer, IReadOnlyList<string> UnverifiedNumbers);

/// <summary>Typed client of the Assistant service (US-34, US-35, US-37, US-38).</summary>
public interface IAssistantApi
{
    /// <summary><c>GET conversations</c>.</summary>
    Task<ApiResult<IReadOnlyList<ConversationDto>>> ListConversationsAsync(CancellationToken ct = default);

    /// <summary><c>POST conversations</c>.</summary>
    Task<ApiResult<ConversationDto>> CreateConversationAsync(string? title, CancellationToken ct = default);

    /// <summary><c>DELETE conversations/{id}</c>.</summary>
    Task<ApiResult> DeleteConversationAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET conversations/{id}/messages</c>: user and assistant messages.</summary>
    Task<ApiResult<IReadOnlyList<MessageDto>>> ListMessagesAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST conversations/{id}/messages</c>: 503 when the model is unavailable, 429 over the daily limit.</summary>
    Task<ApiResult<ChatReply>> SendAsync(Guid id, string content, CancellationToken ct = default);

    /// <summary><c>POST conversations/{id}/actions/{actionId}/confirm</c>: 409 when expired or used.</summary>
    Task<ApiResult<ConfirmActionResponse>> ConfirmAsync(Guid id, Guid actionId, CancellationToken ct = default);

    /// <summary><c>POST conversations/{id}/actions/{actionId}/decline</c>.</summary>
    Task<ApiResult> DeclineAsync(Guid id, Guid actionId, CancellationToken ct = default);

    /// <summary><c>GET usage/me</c>.</summary>
    Task<ApiResult<UsageDto>> GetUsageAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AssistantApi(ApiClientFactory api, ApiJsonOptions json) : IAssistantApi
{
    /// <summary>Kind of the only proposal the assistant makes.</summary>
    public const string CreateBatch = "create_batch";

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<ConversationDto>>> ListConversationsAsync(CancellationToken ct = default) =>
        api.Assistant.GetAsync<IReadOnlyList<ConversationDto>>("api/v1/conversations", ct);

    /// <inheritdoc />
    public Task<ApiResult<ConversationDto>> CreateConversationAsync(string? title, CancellationToken ct = default) =>
        api.Assistant.PostAsync<CreateConversationRequest, ConversationDto>("api/v1/conversations", new CreateConversationRequest(title), ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteConversationAsync(Guid id, CancellationToken ct = default) =>
        api.Assistant.DeleteAsync($"api/v1/conversations/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<MessageDto>>> ListMessagesAsync(Guid id, CancellationToken ct = default) =>
        api.Assistant.GetAsync<IReadOnlyList<MessageDto>>($"api/v1/conversations/{id}/messages", ct);

    /// <inheritdoc />
    public async Task<ApiResult<ChatReply>> SendAsync(Guid id, string content, CancellationToken ct = default) =>
        await api.Assistant.PostAsync<SendMessageRequest, ChatReplyDto>($"api/v1/conversations/{id}/messages", new SendMessageRequest(content), ct) switch
        {
            ApiResult<ChatReplyDto>.Success { Value: var reply } => new ApiResult<ChatReply>.Success(Map(reply)),
            ApiResult<ChatReplyDto>.Failure { Error: var error } => new ApiResult<ChatReply>.Failure(error),
            _ => throw new InvalidOperationException(),
        };

    /// <inheritdoc />
    public Task<ApiResult<ConfirmActionResponse>> ConfirmAsync(Guid id, Guid actionId, CancellationToken ct = default) =>
        api.Assistant.PostEmptyAsync<ConfirmActionResponse>($"api/v1/conversations/{id}/actions/{actionId}/confirm", ct);

    /// <inheritdoc />
    public Task<ApiResult> DeclineAsync(Guid id, Guid actionId, CancellationToken ct = default) =>
        api.Assistant.PostAsync($"api/v1/conversations/{id}/actions/{actionId}/decline", ct);

    /// <inheritdoc />
    public Task<ApiResult<UsageDto>> GetUsageAsync(CancellationToken ct = default) =>
        api.Assistant.GetAsync<UsageDto>("api/v1/usage/me", ct);

    private ChatReply Map(ChatReplyDto reply) => new(
        reply.Message,
        reply.PendingAction is { } action ? new BatchProposal(action.ActionId, action.Kind, action.ExpiresAt, MapPlan(action)) : null,
        reply.Disclaimer,
        reply.UnverifiedNumbers);

    // The plan is the only JsonElement in the client: mapped here, once, to the batch contract (rule 9).
    private BatchDto? MapPlan(PendingActionDto action)
    {
        if (action.Kind != CreateBatch || action.Plan.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return action.Plan.Deserialize(json.TypeInfo<BatchDto>());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
