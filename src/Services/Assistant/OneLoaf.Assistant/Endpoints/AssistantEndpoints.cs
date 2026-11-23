using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using OneLoaf.Assistant.Application.Analysis;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.ServiceDefaults.Security;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.Assistant.Endpoints;

/// <summary>Names used by the assistant endpoints.</summary>
public static class AssistantApi
{
    /// <summary>OpenAPI tag of the chat.</summary>
    public const string ChatTag = "Chat";

    /// <summary>OpenAPI tag of the analysis and usage.</summary>
    public const string InsightsTag = "Insights";

    /// <summary>Rate limiting policy of the message endpoint.</summary>
    public const string MessagesPolicy = "assistant-messages";

    /// <summary>Route of the conversations.</summary>
    public const string ConversationsRoute = Routes.ApiV1 + "/conversations";
}

/// <summary>Chat with the assistant, plan confirmation, bake analysis and usage (US-34 - US-38).</summary>
public static class AssistantEndpoints
{
    private const string TooManyMessagesTitle = "Too many messages. Wait a minute and try again.";

    /// <summary>Per-user limit of the message endpoint; rejections are 429 ProblemDetails.</summary>
    public static IServiceCollection AddAssistantRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(AssistantApi.MessagesPolicy, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<AssistantOptions>();
                var user = httpContext.User.FindFirst(ClaimTypes.Subject)?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(user, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.MessagesPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
            limiter.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers[HeaderNames.RetryAfter] =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Type = ProblemTypes.TooManyRequests,
                        Title = TooManyMessagesTitle,
                    },
                });
            };
        });
        return services;
    }

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var chat = app.MapGroup(AssistantApi.ConversationsRoute).WithTags(AssistantApi.ChatTag).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        chat.MapGet("/", async (ChatService s, CancellationToken ct) => TypedResults.Ok(await s.ListAsync(ct)))
            .WithName("ListConversations").WithSummary("List conversations").WithDescription("Newest first.");
        chat.MapPost("/", CreateAsync).WithValidation<CreateConversationRequest>()
            .WithName("CreateConversation").WithSummary("Start a conversation")
            .WithDescription("The first assistant answer says that it is an AI.");
        chat.MapDelete("/{id:guid}", DeleteAsync).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteConversation").WithSummary("Delete a conversation and its messages").WithDescription("Erasure of chat data.");
        chat.MapGet("/{id:guid}/messages", async (Guid id, [AsParameters] MessageListQuery query, ChatService s, CancellationToken ct) =>
                TypedResults.Ok(await s.GetMessagesAsync(id, query, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListMessages").WithSummary("Messages of a conversation")
            .WithDescription("User and assistant messages; includeTools=true adds tool calls with their input and output (audit, US-35).");
        chat.MapPost("/{id:guid}/messages", async (Guid id, SendMessageRequest request, ChatService s, CancellationToken ct) =>
                TypedResults.Ok(await s.SendAsync(id, request, ct)))
            .WithValidation<SendMessageRequest>()
            .RequireRateLimiting(AssistantApi.MessagesPolicy)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("SendMessage").WithSummary("Send a message to the assistant")
            .WithDescription("US-34 - US-38. Tools call the Core API with your token; numbers come only from tool results (US-35). " +
                             "A batch proposal comes back as pendingAction and is saved only after confirmation.");
        chat.MapPost("/{id:guid}/actions/{actionId:guid}/confirm", async (Guid id, Guid actionId, ChatService s, CancellationToken ct) =>
                TypedResults.Ok(await s.ConfirmAsync(id, actionId, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ConfirmAction").WithSummary("Confirm a batch proposal")
            .WithDescription("US-34. Saves the proposed batch in the Core API with source = assistant; 409 when expired or used.");
        chat.MapPost("/{id:guid}/actions/{actionId:guid}/decline", DeclineAsync)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("DeclineAction").WithSummary("Decline a batch proposal").WithDescription("Nothing is saved.");

        app.MapPost(Routes.ApiV1 + "/bake-records/{bakeRecordId:guid}/analysis",
                async (Guid bakeRecordId, AnalysisService s, CancellationToken ct) => TypedResults.Ok(await s.AnalyseAsync(bakeRecordId, ct)))
            .WithTags(AssistantApi.InsightsTag).RequireAuthorization()
            .RequireRateLimiting(AssistantApi.MessagesPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("AnalyseBake").WithSummary("Analyse a finished bake")
            .WithDescription("US-36. Probable causes from the planned vs actual stage log and the feedback; stored in the record. " +
                             "422 without feedback, 503 when the model is unavailable (record unchanged).");

        app.MapGet(Routes.ApiV1 + "/usage/me", async (UsageGuard s, CancellationToken ct) => TypedResults.Ok(await s.GetTodayAsync(ct)))
            .WithTags(AssistantApi.InsightsTag).RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetMyUsage").WithSummary("Today's assistant usage").WithDescription("Requests and tokens against the daily limits.");
        return app;
    }

    private static async Task<Created<ConversationDto>> CreateAsync(CreateConversationRequest request, ChatService chat, CancellationToken ct)
    {
        var conversation = await chat.CreateAsync(request, ct);
        return TypedResults.Created($"{AssistantApi.ConversationsRoute}/{conversation.Id}", conversation);
    }

    private static async Task<NoContent> DeleteAsync(Guid id, ChatService chat, CancellationToken ct)
    {
        await chat.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeclineAsync(Guid id, Guid actionId, ChatService chat, CancellationToken ct)
    {
        await chat.DeclineAsync(id, actionId, ct);
        return TypedResults.NoContent();
    }
}

/// <summary>A type of the assistant assembly, used to locate the entry point by the test host.</summary>
public sealed class AssistantEntryPoint;
