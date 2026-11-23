using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Assistant.Application.Analysis;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Application.Verification;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Assistant.UnitTests;

public sealed class AssistantTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RecipeId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeConversations _conversations = new();
    private readonly FakeCoreApi _coreApi = new();
    private readonly FakePendingActions _pending = new();
    private readonly FakeUsage _usage = new();
    private readonly AssistantOptions _options = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- US-35 number provenance ----

    [Fact]
    public void US35_Verifier_AcceptsToolAndUserNumbers_RejectsInventedOnes()
    {
        var tool = """{"total":11.22,"doughWeightG":2880,"readyFrom":"2026-11-21T02:00:00+00:00","title":"Loaf T550"}""";

        var ok = NumberProvenanceVerifier.Verify("3 loaves cost 11.22 EUR, 2.88 kg of dough, ready at 02:00 on 2026-11-21 (T550).",
            [tool], ["make 3 loaves"], TimeZoneInfo.Utc);
        var invented = NumberProvenanceVerifier.Verify("It costs about 12.50 and takes 15 hours.", [tool], [], TimeZoneInfo.Utc);

        Assert.True(ok.IsValid, string.Join(",", ok.Unverified));
        Assert.Equal(["12.5", "15"], invented.Unverified);
        Assert.Equal("It costs about [?] and takes [?] hours.", NumberProvenanceVerifier.Redact("It costs about 12.50 and takes 15 hours.", invented.Unverified));
    }

    [Fact]
    public void Verifier_RoundsAndCommaDecimals()
    {
        var result = NumberProvenanceVerifier.Verify("Rounded 1,8 and 2 steps.", ["""{"amount":1.8004}"""], [], TimeZoneInfo.Utc);

        Assert.True(result.IsValid);
    }

    // ---- Budget ----

    [Fact]
    public async Task UsageGuard_DailyLimitAndMonthlyBudget_Throw429()
    {
        var guard = Guard();
        _usage.Daily = new DailyUsage(_options.DailyRequestLimit, 0, 0);
        var daily = await Assert.ThrowsAsync<TooManyRequestsException>(() => guard.EnsureAllowedAsync(Ct));
        Assert.Equal(TimeSpan.FromHours(15), daily.RetryAfter);

        _usage.Daily = new DailyUsage(0, 0, 0);
        _usage.Monthly = _options.MonthlyBudgetUsd;
        var budget = await Assert.ThrowsAsync<TooManyRequestsException>(() => guard.EnsureAllowedAsync(Ct));
        Assert.Equal(UsageGuard.BudgetMessage, budget.Message);

        Assert.Equal(0.0016m, guard.Cost(1000, 100, 1000));
        await guard.RecordAsync(1000, 100, 0.0016m, Ct);
        Assert.Equal(1100, _usage.AddedTokens);
        Assert.Equal(_options.DailyTokenLimit, (await guard.GetTodayAsync(Ct)).TokenLimit);
    }

    // ---- US-34 conversation loop ----

    [Fact]
    public async Task US34_Loop_FindsRecipeProposesBatchAndReturnsPendingAction()
    {
        var conversation = await _conversations.CreateAsync(UserId, null, Ct);
        _coreApi.Responses["/api/v1/recipes?pageSize=10&search=white"] = """{"items":[{"id":"%RECIPE%","title":"White loaf"}]}""";
        _coreApi.Responses["/api/v1/batches/preview"] = """{"totalCost":11.22,"items":[{"loaves":3}]}""";
        var claude = new ScriptedClaude(
            ToolUse(ToolNames.FindRecipes, new { query = "white" }),
            ToolUse(ToolNames.ProposeBatch, new { plannedStartAt = "2026-11-21T12:00:00+00:00", items = new[] { new { recipeId = RecipeId, loaves = 3 } } }),
            Text("Plan: 3 loaves, cost 11.22. Please confirm."));

        var reply = await Chat(claude).SendAsync(conversation.Id, new SendMessageRequest("3 white loaves tomorrow at 12"), Ct);

        Assert.Equal("Plan: 3 loaves, cost 11.22. Please confirm.", reply.Message.Content);
        Assert.NotNull(reply.PendingAction);
        Assert.Equal([ToolNames.FindRecipes, ToolNames.ProposeBatch], reply.ToolCalls.Select(t => t.Name));
        Assert.Null(reply.Disclaimer);
        Assert.Empty(reply.UnverifiedNumbers);
        var stored = Assert.Single(_pending.Actions.Values);
        Assert.Contains("\"source\":\"assistant\"", stored.Payload, StringComparison.Ordinal);
        Assert.Equal(2, _conversations.Messages.Count(m => m.Role == ChatRole.Tool));
        Assert.Equal(1, _usage.AddedRequests);
    }

    [Fact]
    public async Task Loop_InventedNumber_GetsOneCorrectionThenIsRedacted()
    {
        var conversation = await _conversations.CreateAsync(UserId, null, Ct);
        var claude = new ScriptedClaude(Text("It needs 450 g."), Text("Still 450 g."));

        var reply = await Chat(claude).SendAsync(conversation.Id, new SendMessageRequest("How much flour?"), Ct);

        Assert.Equal("Still [?] g.", reply.Message.Content);
        Assert.Equal(["450"], reply.UnverifiedNumbers);
        Assert.Equal(Prompts.AdviceDisclaimer, reply.Disclaimer);
        Assert.Equal(2, claude.Requests.Count);
    }

    [Fact]
    public async Task Loop_RefusalAndRoundLimitAndUnknownTool()
    {
        var conversation = await _conversations.CreateAsync(UserId, null, Ct);
        _options.MaxToolRounds = 1;

        var refusal = await Chat(new ScriptedClaude(new ClaudeResponse(StopReasons.Refusal, [], 1, 1, 0)))
            .SendAsync(conversation.Id, new SendMessageRequest("x"), Ct);
        var limited = await Chat(new ScriptedClaude(ToolUse("no_such_tool", new { }), ToolUse(ToolNames.GetMissingData, new { })))
            .SendAsync(conversation.Id, new SendMessageRequest("y"), Ct);

        Assert.Equal(Prompts.RefusalAnswer, refusal.Message.Content);
        Assert.Equal(Prompts.RoundLimitAnswer, limited.Message.Content);
        Assert.False(limited.ToolCalls[0].Succeeded);
    }

    [Fact]
    public async Task Confirm_SavesThroughCoreApi_DeclineAndExpiredFail()
    {
        var conversation = await _conversations.CreateAsync(UserId, null, Ct);
        var batchId = Guid.NewGuid();
        _coreApi.Responses["/api/v1/batches"] = $$"""{"id":"{{batchId}}"}""";
        var action = new PendingAction(Guid.NewGuid(), UserId, conversation.Id, PendingActionKinds.CreateBatch, "{}", Now, Now.AddMinutes(30));
        var expired = action with { Id = Guid.NewGuid(), ExpiresAt = Now.AddMinutes(-1) };
        var declined = action with { Id = Guid.NewGuid() };
        _pending.Actions[action.Id] = action;
        _pending.Actions[expired.Id] = expired;
        _pending.Actions[declined.Id] = declined;
        var chat = Chat(new ScriptedClaude());

        var confirmed = await chat.ConfirmAsync(conversation.Id, action.Id, Ct);
        await chat.DeclineAsync(conversation.Id, declined.Id, Ct);

        Assert.Equal(batchId, confirmed.BatchId);
        Assert.False(_pending.Actions.ContainsKey(declined.Id));
        await Assert.ThrowsAsync<ConflictException>(() => chat.ConfirmAsync(conversation.Id, action.Id, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => chat.ConfirmAsync(conversation.Id, expired.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => chat.ConfirmAsync(Guid.NewGuid(), action.Id, Ct));
    }

    [Fact]
    public async Task Conversations_ListCreateDeleteAndForeign()
    {
        var chat = Chat(new ScriptedClaude());
        var created = await chat.CreateAsync(new CreateConversationRequest(" Weekend "), Ct);
        var foreign = await _conversations.CreateAsync(Guid.NewGuid(), null, Ct);

        Assert.Equal("Weekend", created.Title);
        Assert.Single(await chat.ListAsync(Ct));
        Assert.Empty(await chat.GetMessagesAsync(created.Id, new MessageListQuery(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => chat.GetMessagesAsync(foreign.Id, new MessageListQuery(), Ct));
        await chat.DeleteAsync(created.Id, Ct);
        await Assert.ThrowsAsync<NotFoundException>(() => chat.DeleteAsync(created.Id, Ct));
    }

    // ---- US-36 analysis ----

    [Fact]
    public async Task US36_Analysis_RequiresFeedbackAndStoresTheText()
    {
        var record = Guid.NewGuid();
        _coreApi.Responses[$"/api/v1/bake-records/{record}"] = """{"comment":"Dense crumb","rating":3}""";
        _coreApi.Responses[$"/api/v1/bake-records/{record}/stage-log"] = """[{"stageName":"Bulk","deviationMinutes":40}]""";
        _coreApi.Responses[$"/api/v1/bake-records/{record}/analysis"] = "{}";
        var claude = new ScriptedClaude(ToolUse(ToolNames.GetBakeAnalysisData, new { bakeRecordId = record }), Text("Bulk ran 40 min long."));

        var analysis = await Analysis(claude).AnalyseAsync(record, Ct);

        Assert.Equal("Bulk ran 40 min long.", analysis.Analysis);
        Assert.Contains(_coreApi.Calls, c => c == $"PUT /api/v1/bake-records/{record}/analysis");

        var noFeedback = Guid.NewGuid();
        _coreApi.Responses[$"/api/v1/bake-records/{noFeedback}"] = """{"comment":null}""";
        await Assert.ThrowsAsync<BusinessRuleException>(() => Analysis(claude).AnalyseAsync(noFeedback, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => Analysis(claude).AnalyseAsync(Guid.NewGuid(), Ct));
    }

    // ---- Tools ----

    [Fact]
    public async Task Tools_ValidateInputAndMapCoreApiErrors()
    {
        var context = new ToolContext(UserId, Guid.NewGuid());
        _coreApi.Responses[$"/api/v1/recipes/{RecipeId}/cost?loaves=3&asOf=2026-09-15"] = """{"total":10.5}""";
        _coreApi.Status[$"/api/v1/recipes/{RecipeId}"] = HttpStatusCode.NotFound;
        _coreApi.Responses[$"/api/v1/recipes/{RecipeId}"] = """{"title":"The recipe was not found."}""";

        var cost = await new CalculateCostTool(_coreApi).ExecuteAsync(Json(new { recipeId = RecipeId, loaves = 3, asOf = "2026-09-15" }), context, Ct);
        var badInput = await new CalculateCostTool(_coreApi).ExecuteAsync(Json(new { recipeId = "x", loaves = 0 }), context, Ct);
        var notFound = await new GetRecipeTool(_coreApi).ExecuteAsync(Json(new { recipeId = RecipeId }), context, Ct);
        var timeline = await new CalculateTimelineTool(_coreApi).ExecuteAsync(Json(new { recipeId = RecipeId, start = "2026-11-21T12:00:00" }), context, Ct);

        Assert.False(cost.IsError);
        Assert.True(badInput.IsError);
        Assert.Contains("404: The recipe was not found.", notFound.Content, StringComparison.Ordinal);
        Assert.True(timeline.IsError);
    }

    private ChatService Chat(ScriptedClaude claude) => new(_conversations, claude, Registry(), _coreApi, _pending, Guard(), _options,
        new FixedTime(Now), new User(), NullLogger<ChatService>.Instance);

    private AnalysisService Analysis(ScriptedClaude claude) =>
        new(claude, Registry(), _coreApi, Guard(), new FixedTime(Now), new User(), NullLogger<AnalysisService>.Instance);

    private UsageGuard Guard() => new(_usage, _options, new FixedTime(Now), new User());

    private ToolRegistry Registry() => new([
        new FindRecipesTool(_coreApi), new GetRecipeTool(_coreApi), new CalculateCostTool(_coreApi), new GetMissingDataTool(_coreApi),
        new ProposeBatchTool(_coreApi, _pending, new FixedTime(Now)), new GetBakeAnalysisDataTool(_coreApi),
    ]);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static ClaudeResponse ToolUse(string name, object input) =>
        new(StopReasons.ToolUse, [new ClaudeToolUseBlock(Guid.NewGuid().ToString("N"), name, Json(input))], 100, 20, 50);

    private static ClaudeResponse Text(string text) => new(StopReasons.EndTurn, [new ClaudeTextBlock(text)], 100, 20, 0);

    private sealed class ScriptedClaude(params ClaudeResponse[] responses) : IClaudeClient
    {
        private readonly Queue<ClaudeResponse> _responses = new(responses);

        public List<ClaudeRequest> Requests { get; } = [];

        public Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FakeCoreApi : ICoreApiClient
    {
        public Dictionary<string, string> Responses { get; } = new(StringComparer.Ordinal)
        {
            ["/api/v1/kitchen-settings"] = """{"timeZone":"UTC"}""",
            ["/api/v1/warnings"] = "[]",
        };

        public Dictionary<string, HttpStatusCode> Status { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<CoreApiResponse> SendAsync(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken)
        {
            Calls.Add($"{method} {pathAndQuery}");
            var found = Responses.TryGetValue(pathAndQuery, out var json);
            var status = Status.TryGetValue(pathAndQuery, out var s) ? s : found ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            return Task.FromResult(new CoreApiResponse(status, (json ?? "{}").Replace("%RECIPE%", RecipeId.ToString(), StringComparison.Ordinal)));
        }
    }

    private sealed class FakeConversations : IConversationRepository
    {
        private readonly Dictionary<Guid, (Guid Owner, ConversationDto Conversation)> _conversations = [];

        public List<MessageDto> Messages { get; } = [];

        public Task<IReadOnlyList<ConversationDto>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConversationDto>>(_conversations.Values.Where(c => c.Owner == ownerId).Select(c => c.Conversation).ToList());

        public Task<ConversationDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_conversations.TryGetValue(id, out var c) && c.Owner == ownerId ? c.Conversation : null);

        public Task<ConversationDto> CreateAsync(Guid ownerId, string? title, CancellationToken cancellationToken)
        {
            var dto = new ConversationDto(Guid.NewGuid(), title, Now, Now);
            _conversations[dto.Id] = (ownerId, dto);
            return Task.FromResult(dto);
        }

        public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_conversations.TryGetValue(id, out var c) && c.Owner == ownerId && _conversations.Remove(id));

        public Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, bool includeTools, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MessageDto>>(Messages.Where(m => includeTools || m.Role != ChatRole.Tool).ToList());

        public Task<MessageDto> AddMessageAsync(Guid conversationId, NewMessage message, CancellationToken cancellationToken)
        {
            var dto = new MessageDto(Guid.NewGuid(), message.Role, message.Content, message.ToolName, null, Now);
            Messages.Add(dto);
            return Task.FromResult(dto);
        }

        public Task SetTitleIfEmptyAsync(Guid conversationId, string title, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePendingActions : IPendingActionStore
    {
        public Dictionary<Guid, PendingAction> Actions { get; } = [];

        public Task SaveAsync(PendingAction action, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            Actions[action.Id] = action;
            return Task.CompletedTask;
        }

        public Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken) =>
            Task.FromResult(Actions.GetValueOrDefault(actionId));

        public Task<bool> DeleteAsync(Guid actionId, CancellationToken cancellationToken) => Task.FromResult(Actions.Remove(actionId));
    }

    private sealed class FakeUsage : IUsageStore
    {
        public DailyUsage Daily { get; set; } = new(0, 0, 0);

        public decimal Monthly { get; set; }

        public int AddedRequests { get; private set; }

        public long AddedTokens { get; private set; }

        public Task<DailyUsage> GetDailyAsync(Guid userId, DateOnly day, CancellationToken cancellationToken) => Task.FromResult(Daily);

        public Task<decimal> GetMonthlyCostAsync(DateOnly month, CancellationToken cancellationToken) => Task.FromResult(Monthly);

        public Task AddAsync(Guid userId, DateOnly day, long tokens, decimal costUsd, CancellationToken cancellationToken)
        {
            AddedRequests++;
            AddedTokens += tokens;
            return Task.CompletedTask;
        }
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid Id => UserId;

        public string? Role => "user";

        public bool IsAdmin => false;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
