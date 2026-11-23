using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Application.Verification;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Assistant.Application.Chat;

/// <summary>
/// The conversation loop (US-34 - US-38): Claude chooses tools, the tools call the Core API with the baker's token,
/// numbers in the final answer are checked against the tool results, and nothing is saved without confirmation.
/// </summary>
public sealed partial class ChatService(
    IConversationRepository conversations,
    IClaudeClient claude,
    ToolRegistry tools,
    ICoreApiClient coreApi,
    IPendingActionStore pendingActions,
    UsageGuard usageGuard,
    AssistantOptions options,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<ChatService> logger)
{
    /// <summary>Message for a missing conversation.</summary>
    public const string NotFoundMessage = "The conversation was not found.";

    /// <summary>Message for a proposal that is unknown, expired, used or of another user.</summary>
    public const string ActionGoneMessage = "The proposal has expired or was already used.";

    private const int TitleLength = 60;
    private static readonly System.Text.CompositeFormat NumberCorrectionFormat = System.Text.CompositeFormat.Parse(Prompts.NumberCorrection);

    /// <summary>Conversations of the current user.</summary>
    public Task<IReadOnlyList<ConversationDto>> ListAsync(CancellationToken cancellationToken) =>
        conversations.ListAsync(currentUser.Id, cancellationToken);

    /// <summary>Starts a conversation.</summary>
    public Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return conversations.CreateAsync(currentUser.Id, string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(), cancellationToken);
    }

    /// <summary>Deletes a conversation and its messages.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await conversations.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    /// <summary>Messages of a conversation.</summary>
    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid id, MessageListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await EnsureOwnedAsync(id, cancellationToken);
        return await conversations.GetMessagesAsync(id, query.IncludeTools, cancellationToken);
    }

    /// <summary>Handles one message of the baker.</summary>
    public async Task<ChatReplyDto> SendAsync(Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureOwnedAsync(id, cancellationToken);
        await usageGuard.EnsureAllowedAsync(cancellationToken);

        var history = await conversations.GetMessagesAsync(id, includeTools: false, cancellationToken);
        var content = request.Content.Trim();
        await conversations.AddMessageAsync(id, new NewMessage(ChatRole.User, content), cancellationToken);
        await conversations.SetTitleIfEmptyAsync(id, content.Length <= TitleLength ? content : content[..TitleLength], cancellationToken);

        var zone = await GetTimeZoneAsync(cancellationToken);
        var messages = history.TakeLast(options.HistoryWindow)
            .Select(m => ClaudeMessage.Text(m.Role == ChatRole.User ? ClaudeRole.User : ClaudeRole.Assistant, m.Content))
            .Append(ClaudeMessage.Text(ClaudeRole.User, content))
            .ToList();

        var turn = new Turn(new ToolContext(currentUser.Id, id));
        var claudeRequest = new ClaudeRequest(Prompts.System, Prompts.Context(timeProvider.GetUtcNow(), zone), tools.Definitions, messages);
        var answer = await RunLoopAsync(claudeRequest, turn, cancellationToken);

        var userTexts = history.Where(m => m.Role == ChatRole.User).Select(m => m.Content).Append(content).ToList();
        var check = NumberProvenanceVerifier.Verify(answer, turn.ToolOutputs, userTexts, zone);
        if (!check.IsValid)
        {
            messages.Add(ClaudeMessage.Text(ClaudeRole.Assistant, answer));
            messages.Add(ClaudeMessage.Text(ClaudeRole.User,
                string.Format(CultureInfo.InvariantCulture, NumberCorrectionFormat, string.Join(", ", check.Unverified))));
            answer = await RunLoopAsync(claudeRequest with { Messages = messages }, turn, cancellationToken);
            check = NumberProvenanceVerifier.Verify(answer, turn.ToolOutputs, userTexts, zone);
            if (!check.IsValid)
            {
                LogUnverified(logger, id, check.Unverified.Count);
                answer = NumberProvenanceVerifier.Redact(answer, check.Unverified);
            }
        }

        var cost = usageGuard.Cost(turn.InputTokens, turn.OutputTokens, turn.CacheReadTokens);
        var stored = await conversations.AddMessageAsync(id,
            new NewMessage(ChatRole.Assistant, answer, InputTokens: turn.InputTokens, OutputTokens: turn.OutputTokens), cancellationToken);
        await usageGuard.RecordAsync(turn.InputTokens, turn.OutputTokens, cost, cancellationToken);
        LogTurn(logger, id, turn.ToolCalls.Count, turn.InputTokens, turn.OutputTokens);

        return new ChatReplyDto(stored, turn.Context.PendingAction, turn.ToolCalls,
            turn.ToolCalls.Count == 0 ? Prompts.AdviceDisclaimer : null, check.Unverified,
            new TurnUsageDto(turn.InputTokens, turn.OutputTokens, cost));
    }

    /// <summary>Saves the proposed batch through the Core API with source = assistant (US-34).</summary>
    public async Task<ConfirmActionResponse> ConfirmAsync(Guid conversationId, Guid actionId, CancellationToken cancellationToken)
    {
        var action = await GetOwnActionAsync(conversationId, actionId, cancellationToken);
        if (!await pendingActions.DeleteAsync(actionId, cancellationToken))
        {
            throw new ConflictException(ActionGoneMessage);
        }

        using var payload = JsonDocument.Parse(action.Payload);
        var response = await coreApi.SendAsync(HttpMethod.Post, CoreApiRoutes.Batches, payload.RootElement, cancellationToken);
        if (!response.IsSuccess)
        {
            throw new ConflictException(CoreApiTool.ToResult(response).Content);
        }

        using var batch = JsonDocument.Parse(response.Body);
        var batchId = batch.RootElement.GetProperty("id").GetGuid();
        await conversations.AddMessageAsync(conversationId, new NewMessage(ChatRole.Assistant, Prompts.BatchSavedMessage), cancellationToken);
        LogConfirmed(logger, batchId, currentUser.Id);
        return new ConfirmActionResponse(batchId);
    }

    /// <summary>Discards a proposal; nothing is saved (UC-01 alternative flow A2).</summary>
    public async Task DeclineAsync(Guid conversationId, Guid actionId, CancellationToken cancellationToken)
    {
        await GetOwnActionAsync(conversationId, actionId, cancellationToken);
        await pendingActions.DeleteAsync(actionId, cancellationToken);
    }

    /// <summary>Runs Claude with tools until it answers or the round limit is reached; returns the final text.</summary>
    internal async Task<string> RunLoopAsync(ClaudeRequest request, Turn turn, CancellationToken cancellationToken)
    {
        var messages = request.Messages.ToList();
        for (var round = 0; round <= options.MaxToolRounds; round++)
        {
            var response = await claude.SendAsync(request with { Messages = messages }, cancellationToken);
            turn.Add(response);
            if (response.StopReason == StopReasons.Refusal)
            {
                return Prompts.RefusalAnswer;
            }

            if (response.StopReason != StopReasons.ToolUse || response.ToolUses.Count == 0)
            {
                return response.Text;
            }

            if (round == options.MaxToolRounds)
            {
                break;
            }

            messages.Add(new ClaudeMessage(ClaudeRole.Assistant, response.Content));
            var results = new List<ClaudeBlock>();
            foreach (var use in response.ToolUses)
            {
                var result = await tools.ExecuteAsync(use.Name, use.Input, turn.Context, cancellationToken);
                turn.Record(use.Name, result);
                results.Add(new ClaudeToolResultBlock(use.Id, result.Content, result.IsError));
                await conversations.AddMessageAsync(turn.Context.ConversationId, new NewMessage(ChatRole.Tool,
                    result.IsError ? "error" : "ok", use.Name,
                    JsonSerializer.Serialize(new { input = use.Input, output = Parse(result.Content) })), cancellationToken);
            }

            messages.Add(new ClaudeMessage(ClaudeRole.User, results));
        }

        return Prompts.RoundLimitAnswer;
    }

    private static JsonElement Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(json);
        }
    }

    private async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken)
    {
        var response = await coreApi.SendAsync(HttpMethod.Get, CoreApiRoutes.KitchenSettings, null, cancellationToken);
        if (response.IsSuccess)
        {
            using var settings = JsonDocument.Parse(response.Body);
            if (settings.RootElement.TryGetProperty("timeZone", out var tz) && tz.GetString() is { } id
                && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.Utc;
    }

    private async Task EnsureOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(currentUser.Id, id, cancellationToken) is null)
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    private async Task<PendingAction> GetOwnActionAsync(Guid conversationId, Guid actionId, CancellationToken cancellationToken)
    {
        await EnsureOwnedAsync(conversationId, cancellationToken);
        var action = await pendingActions.GetAsync(actionId, cancellationToken);
        if (action is null || action.UserId != currentUser.Id || action.ConversationId != conversationId
            || action.ExpiresAt <= timeProvider.GetUtcNow())
        {
            throw new ConflictException(ActionGoneMessage);
        }

        return action;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Conversation {ConversationId}: {ToolCalls} tool calls, {InputTokens} in / {OutputTokens} out")]
    private static partial void LogTurn(ILogger logger, Guid conversationId, int toolCalls, int inputTokens, int outputTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation {ConversationId}: {Count} unverified numbers removed")]
    private static partial void LogUnverified(ILogger logger, Guid conversationId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch {BatchId} saved from the assistant for {UserId}")]
    private static partial void LogConfirmed(ILogger logger, Guid batchId, Guid userId);
}

/// <summary>Accumulated state of one turn: tokens, tool calls and tool outputs.</summary>
internal sealed class Turn(ToolContext context)
{
    public ToolContext Context { get; } = context;

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public int CacheReadTokens { get; private set; }

    public List<ToolCallDto> ToolCalls { get; } = [];

    public List<string> ToolOutputs { get; } = [];

    public void Add(ClaudeResponse response)
    {
        InputTokens += response.InputTokens;
        OutputTokens += response.OutputTokens;
        CacheReadTokens += response.CacheReadTokens;
    }

    public void Record(string tool, ToolResult result)
    {
        ToolCalls.Add(new ToolCallDto(tool, !result.IsError));
        if (!result.IsError)
        {
            ToolOutputs.Add(result.Content);
        }
    }
}
