using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.IntegrationTests.Assistant;

/// <summary>UC-01 end to end: the assistant plans a batch with real Core API calls and saves it only after confirmation.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantFlowTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UC01_PlanWithTools_ConfirmSavesBatchWithSourceAssistant()
    {
        string? quotedCost = null;
        var claude = new ScriptedClaude(
            _ => ToolUse(ToolNames.FindRecipes, new { query = "White homemade" }),
            request =>
            {
                using var found = JsonDocument.Parse(ScriptedClaude.LastToolResult(request));
                var recipeId = found.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
                return ToolUse(ToolNames.ProposeBatch, new
                {
                    name = "Assistant batch",
                    plannedStartAt = "2030-05-01T12:00:00+00:00",
                    items = new[] { new { recipeId, loaves = 3 } },
                });
            },
            request =>
            {
                using var proposal = JsonDocument.Parse(ScriptedClaude.LastToolResult(request));
                quotedCost = proposal.RootElement.GetProperty("plan").GetProperty("totalCost").GetDecimal().ToString(CultureInfo.InvariantCulture);
                return Text($"I am an AI assistant. Plan for 3 loaves: total cost {quotedCost} EUR. Please confirm.");
            });
        var assistant = fixture.CreateAssistant(claude);
        using var client = AuthorizedClient(assistant);

        var conversation = await (await client.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest(), CoreApiFactory.Json, Ct))
            .ReadAsync<ConversationDto>();
        var reply = await (await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages",
            new SendMessageRequest("Create a batch of 3 white homemade loaves for May 1st 2030 at 12"), CoreApiFactory.Json, Ct))
            .ReadAsync<ChatReplyDto>();

        Assert.NotNull(reply.PendingAction);
        Assert.Empty(reply.UnverifiedNumbers);
        Assert.Contains(quotedCost!, reply.Message.Content, StringComparison.Ordinal);
        Assert.Equal([ToolNames.FindRecipes, ToolNames.ProposeBatch], reply.ToolCalls.Select(t => t.Name));

        // Nothing is saved before the confirmation.
        using var core = fixture.CreateClientAs(Seed.Anna);
        var calendarBefore = await core.GetStringAsync("/api/v1/batches?pageSize=100", Ct);
        Assert.DoesNotContain("Assistant batch", calendarBefore, StringComparison.Ordinal);

        var confirmed = await (await client.PostAsync(
            $"/api/v1/conversations/{conversation.Id}/actions/{reply.PendingAction!.ActionId}/confirm", null, Ct)).ReadAsync<ConfirmActionResponse>();
        var batch = await (await core.GetAsync($"/api/v1/batches/{confirmed.BatchId}", Ct)).ReadAsync<BatchDto>();
        Assert.Equal(BatchSource.Assistant, batch.Source);
        Assert.Equal(3, batch.Items[0].Loaves);

        using var again = await client.PostAsync($"/api/v1/conversations/{conversation.Id}/actions/{reply.PendingAction.ActionId}/confirm", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var audit = await (await client.GetAsync($"/api/v1/conversations/{conversation.Id}/messages?includeTools=true", Ct))
            .ReadAsync<List<MessageDto>>();
        Assert.Equal(2, audit.Count(m => m.Role == ChatRole.Tool));
    }

    [Fact]
    public async Task UC01A3_ClaudeUnavailable_Returns503()
    {
        var assistant = fixture.CreateAssistant(new ScriptedClaude(_ => throw new ExternalServiceUnavailableException("Claude is down.")));
        using var client = AuthorizedClient(assistant);
        var conversation = await (await client.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest(), CoreApiFactory.Json, Ct))
            .ReadAsync<ConversationDto>();

        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages",
            new SendMessageRequest("Plan a batch"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task US03_OtherUsersConversation_Returns404()
    {
        var assistant = fixture.CreateAssistant(new ScriptedClaude());
        using var anna = AuthorizedClient(assistant);
        var conversation = await (await anna.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest("Private"), CoreApiFactory.Json, Ct))
            .ReadAsync<ConversationDto>();
        using var boris = AuthorizedClient(assistant, Seed.Boris);

        using var read = await boris.GetAsync($"/api/v1/conversations/{conversation.Id}/messages", Ct);
        using var delete = await boris.DeleteAsync($"/api/v1/conversations/{conversation.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private HttpClient AuthorizedClient(AssistantFactory assistant, Guid? user = null)
    {
        using var core = fixture.CreateClientAs(user ?? Seed.Anna);
        var client = assistant.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", core.DefaultRequestHeaders.Authorization!.Parameter);
        return client;
    }

    private static ClaudeResponse ToolUse(string name, object input) =>
        new(StopReasons.ToolUse, [new ClaudeToolUseBlock(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(input))], 900, 60, 0);

    private static ClaudeResponse Text(string text) => new(StopReasons.EndTurn, [new ClaudeTextBlock(text)], 1200, 80, 0);
}
