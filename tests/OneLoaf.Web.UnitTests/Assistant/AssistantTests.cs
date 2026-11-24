using System.Net;
using System.Text.Json.Nodes;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Web.Features.Assistant;
using OneLoaf.Web.Features.Assistant.Components;
using OneLoaf.Web.Features.Assistant.Pages;
using OneLoaf.Web.Features.Assistant.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.UnitTests.Api;

namespace OneLoaf.Web.UnitTests.Assistant;

public sealed class AssistantTests : BunitContext
{
    private static readonly Guid ActionId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private readonly Api _api = new();
    private readonly List<object> _dispatched = [];

    public AssistantTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<OneLoaf.Web.Shared.Device.DeviceContext>();
        Services.AddSingleton<IAssistantApi>(_api);
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly, typeof(AssistantFeature).Assembly));
        Render<Fluxor.Blazor.Web.StoreInitializer>();
        Services.GetRequiredService<IActionSubscriber>().SubscribeToAction<BatchCreated>(this, a => _dispatched.Add(a));
    }

    [Fact]
    public void PlanCard_PendingProposal_ShowsApiFiguresAndRaisesConfirm()
    {
        var confirmed = false;
        var cut = Render<PlanCard>(p => p
            .Add(x => x.Proposal, Proposal())
            .Add(x => x.OnConfirm, () => confirmed = true));

        Assert.Contains("Saturday bake", cut.Find(".ol-plan__title").TextContent);
        Assert.Contains("200 g", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("7.98", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Nothing is saved until you confirm", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm plan", StringComparison.Ordinal)).Click();
        Assert.True(confirmed);
    }

    [Fact]
    public void Confirm_ProposalFromReply_SavesOnlyAfterConfirmAndLinksTheBatch()
    {
        var cut = Render<AssistantPage>();
        cut.Find("textarea").Input("Plan 2 loaves for Saturday");
        cut.Find("form.ol-composer").Submit();

        cut.WaitForAssertion(() => cut.Find(".ol-plan"));
        Assert.Equal(0, _api.Confirms);
        Assert.Contains("Plan 2 loaves for Saturday", cut.Find(".ol-msg--user").TextContent);

        cut.FindAll(".ol-plan button").Single(b => b.TextContent.Contains("Confirm plan", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("Saved as a batch", cut.Find(".ol-plan").TextContent));
        Assert.Equal(1, _api.Confirms);
        Assert.Equal($"batches/{BatchId}", cut.Find(".ol-plan a").GetAttribute("href"));
        Assert.Contains(_dispatched, a => a is BatchCreated { BatchId: var id } && id == BatchId);
    }

    [Fact]
    public void Send_AssistantUnavailable_OffersTheManualForm()
    {
        _api.Reply = new ApiResult<ChatReply>.Failure(ApiError.FromStatus(503) with
        {
            Detail = "The assistant is temporarily unavailable. Use the planning form or try again later.",
        });
        var cut = Render<AssistantPage>();
        cut.Find("textarea").Input("When should I start?");
        cut.Find("form.ol-composer").Submit();

        cut.WaitForAssertion(() => Assert.Contains("temporarily unavailable", cut.Markup, StringComparison.Ordinal));
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == "batches/new" && a.TextContent.Contains("Use the manual form", StringComparison.Ordinal));
        Assert.Equal("When should I start?", cut.Find("textarea").GetAttribute("value") ?? cut.Find("textarea").TextContent);
    }

    [Fact]
    public async Task SendAsync_RecordedProposal_MapsThePlanToABatch()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "api", "examples", "assistant", "SendMessage.json");
        var body = JsonNode.Parse(await File.ReadAllTextAsync(path, Xunit.TestContext.Current.CancellationToken))!.AsArray()
            .First(e => (int)e!["status"]! == 200)!["responseBody"]!.ToJsonString();
        var stub = StubHttpHandler.Json(HttpStatusCode.OK, body);
        var api = new AssistantApi(new ApiClientFactory(new Clients(stub), PipelineFactory.Json, NullLogger<ApiClient>.Instance), PipelineFactory.Json);

        var result = await api.SendAsync(Guid.NewGuid(), "Plan 2 loaves", Xunit.TestContext.Current.CancellationToken);

        var reply = Assert.IsType<ApiResult<ChatReply>.Success>(result).Value;
        var batch = reply.Proposal?.Batch;
        Assert.NotNull(batch);
        Assert.Equal("Saturday bake", batch.Name);
        Assert.Equal(7.98m, batch.TotalCost);
        Assert.Equal(200m, batch.Items[0].StarterAmountG);
    }

    private static BatchProposal Proposal() => new(ActionId, AssistantApi.CreateBatch, DateTimeOffset.UtcNow.AddMinutes(30), new BatchDto(
        Guid.Empty, "Saturday bake", "planned", "manual", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(15),
        DateTimeOffset.UtcNow.AddDays(1).AddHours(18), null, null, DateOnly.FromDateTime(DateTime.UtcNow), 7.98m, "EUR", null,
        [new BatchItemDto(Guid.Empty, Guid.NewGuid(), "Country sourdough", 2, null, 200m, 1, null, 1, [])], null));

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OneLoaf.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class Clients(StubHttpHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(stub, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5081/") };
    }

    private sealed class Api : IAssistantApi
    {
        private static readonly ConversationDto Conversation = new(Guid.NewGuid(), null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        public int Confirms { get; private set; }

        public ApiResult<ChatReply> Reply { get; set; } = new ApiResult<ChatReply>.Success(new ChatReply(
            new MessageDto(Guid.NewGuid(), "assistant", "I am an AI assistant. Please confirm the plan.", null, null, DateTimeOffset.UnixEpoch),
            Proposal(), "Informational only.", []));

        public Task<ApiResult<IReadOnlyList<ConversationDto>>> ListConversationsAsync(CancellationToken ct = default) => Ok<IReadOnlyList<ConversationDto>>([]);

        public Task<ApiResult<ConversationDto>> CreateConversationAsync(string? title, CancellationToken ct = default) => Ok(Conversation);

        public Task<ApiResult> DeleteConversationAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<ApiResult<IReadOnlyList<MessageDto>>> ListMessagesAsync(Guid id, CancellationToken ct = default) => Ok<IReadOnlyList<MessageDto>>([]);

        public Task<ApiResult<ChatReply>> SendAsync(Guid id, string content, CancellationToken ct = default) => Task.FromResult(Reply);

        public Task<ApiResult<ConfirmActionResponse>> ConfirmAsync(Guid id, Guid actionId, CancellationToken ct = default)
        {
            Confirms++;
            return Ok(new ConfirmActionResponse(BatchId));
        }

        public Task<ApiResult> DeclineAsync(Guid id, Guid actionId, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<ApiResult<UsageDto>> GetUsageAsync(CancellationToken ct = default) =>
            Ok(new UsageDto(new DateOnly(2026, 11, 23), 1, 50, 100, 100_000, 0.01m));

        private static Task<ApiResult<T>> Ok<T>(T value) => Task.FromResult<ApiResult<T>>(new ApiResult<T>.Success(value));
    }
}
