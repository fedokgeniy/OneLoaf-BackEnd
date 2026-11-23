using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Infrastructure.Claude;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.ServiceDefaults;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Options;
using OneLoaf.SharedKernel.Exceptions;
using StackExchange.Redis;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Core API settings of the assistant.</summary>
public sealed class CoreApiOptions
{
    /// <summary>Configuration section name (<c>Assistant__CoreApiBaseUrl</c>).</summary>
    public const string SectionName = "Assistant";

    /// <summary>Internal URL of the Core API as seen from the assistant container.</summary>
    [Required]
    public string CoreApiBaseUrl { get; set; } = string.Empty;
}

/// <summary>Redis settings (<c>Redis__ConnectionString</c>).</summary>
public sealed class RedisOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>Cache keys used by the assistant.</summary>
public static class CacheKeys
{
    /// <summary>A pending proposal.</summary>
    public static string PendingAction(Guid actionId) => $"ai:pending:{actionId:N}";

    /// <summary>Daily usage counters of a user.</summary>
    public static string DailyUsage(Guid userId, DateOnly day) => $"ai:usage:{userId:N}:{day:yyyyMMdd}";

    /// <summary>Global monthly cost.</summary>
    public static string MonthlyCost(DateOnly month) => $"ai:cost:{month:yyyyMM}";
}

/// <summary>
/// Typed <c>HttpClient</c> for the Core API. Forwards the caller's bearer token (so tools run with the baker's own
/// rights) and, through the registered handler, the correlation id.
/// </summary>
public sealed class CoreApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor) : ICoreApiClient
{
    private const string BearerScheme = "Bearer";
    private const string UnavailableMessage = "The Core API is temporarily unavailable.";
    private static readonly JsonSerializerOptions Json = CreateJson();

    /// <inheritdoc />
    public async Task<CoreApiResponse> SendAsync(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        var authorization = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith(BearerScheme + " ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authorization[(BearerScheme.Length + 1)..]);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return new CoreApiResponse(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(UnavailableMessage, ex);
        }
    }

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Extensions.ConfigureJson(json);
        return json;
    }
}

/// <summary>EF Core implementation of <see cref="IConversationRepository"/>.</summary>
public sealed class ConversationRepository(OneLoafDbContext db) : IConversationRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationDto>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
        (await db.ChatConversations.AsNoTracking().Where(c => c.OwnerId == ownerId).OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    /// <inheritdoc />
    public async Task<ConversationDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var conversation = await db.ChatConversations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.OwnerId == ownerId && c.Id == id, cancellationToken);
        return conversation is null ? null : ToDto(conversation);
    }

    /// <inheritdoc />
    public async Task<ConversationDto> CreateAsync(Guid ownerId, string? title, CancellationToken cancellationToken)
    {
        var entity = new ChatConversation { OwnerId = ownerId, Title = title };
        db.ChatConversations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return (await GetAsync(ownerId, entity.Id, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.ChatConversations.Where(c => c.OwnerId == ownerId && c.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, bool includeTools, CancellationToken cancellationToken)
    {
        var messages = db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (!includeTools)
        {
            messages = messages.Where(m => m.Role != ChatRole.Tool);
        }

        return (await messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MessageDto> AddMessageAsync(Guid conversationId, NewMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var entity = new ChatMessage
        {
            ConversationId = conversationId,
            Role = message.Role,
            Content = message.Content,
            ToolName = message.ToolName,
            ToolPayload = message.ToolPayload,
            InputTokens = message.InputTokens,
            OutputTokens = message.OutputTokens,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatMessages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.ChatConversations.Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken);
        db.ChangeTracker.Clear();
        return ToDto(entity);
    }

    /// <inheritdoc />
    public Task SetTitleIfEmptyAsync(Guid conversationId, string title, CancellationToken cancellationToken) =>
        db.ChatConversations.Where(c => c.Id == conversationId && c.Title == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, title), cancellationToken);

    private static ConversationDto ToDto(ChatConversation c) =>
        new(c.Id, c.Title, Utc(c.CreatedAt), Utc(c.UpdatedAt));

    private static MessageDto ToDto(ChatMessage m) => new(m.Id, m.Role, m.Content, m.ToolName,
        m.ToolPayload is null ? null : JsonDocument.Parse(m.ToolPayload).RootElement.Clone(), Utc(m.CreatedAt));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

/// <summary>Pending proposals in Redis with expiry.</summary>
public sealed class RedisPendingActionStore(IConnectionMultiplexer redis) : IPendingActionStore
{
    /// <inheritdoc />
    public Task SaveAsync(PendingAction action, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return redis.GetDatabase().StringSetAsync(CacheKeys.PendingAction(action.Id), JsonSerializer.Serialize(action), lifetime);
    }

    /// <inheritdoc />
    public async Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().StringGetAsync(CacheKeys.PendingAction(actionId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PendingAction>(value.ToString());
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid actionId, CancellationToken cancellationToken) =>
        redis.GetDatabase().KeyDeleteAsync(CacheKeys.PendingAction(actionId));
}

/// <summary>Usage counters in Redis (daily per user, monthly global), stored as micro-dollars.</summary>
public sealed class RedisUsageStore(IConnectionMultiplexer redis, AssistantMetrics metrics) : IUsageStore
{
    private const string Requests = "requests";
    private const string Tokens = "tokens";
    private const string CostMicro = "cost_micro";
    private const decimal Micro = 1_000_000m;
    private static readonly TimeSpan DailyTtl = TimeSpan.FromHours(48);
    private static readonly TimeSpan MonthlyTtl = TimeSpan.FromDays(40);

    /// <inheritdoc />
    public async Task<DailyUsage> GetDailyAsync(Guid userId, DateOnly day, CancellationToken cancellationToken)
    {
        var values = await redis.GetDatabase().HashGetAsync(CacheKeys.DailyUsage(userId, day), [Requests, Tokens, CostMicro]);
        return new DailyUsage((int)ToLong(values[0]), ToLong(values[1]), ToLong(values[2]) / Micro);
    }

    /// <inheritdoc />
    public async Task<decimal> GetMonthlyCostAsync(DateOnly month, CancellationToken cancellationToken) =>
        ToLong(await redis.GetDatabase().StringGetAsync(CacheKeys.MonthlyCost(month))) / Micro;

    /// <inheritdoc />
    public async Task AddAsync(Guid userId, DateOnly day, long tokens, decimal costUsd, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = CacheKeys.DailyUsage(userId, day);
        var micro = (long)Math.Round(costUsd * Micro);
        metrics.Turn(tokens, costUsd);
        await db.HashIncrementAsync(key, Requests);
        await db.HashIncrementAsync(key, Tokens, tokens);
        await db.HashIncrementAsync(key, CostMicro, micro);
        await db.KeyExpireAsync(key, DailyTtl);
        var monthKey = CacheKeys.MonthlyCost(new DateOnly(day.Year, day.Month, 1));
        await db.StringIncrementAsync(monthKey, micro);
        await db.KeyExpireAsync(monthKey, MonthlyTtl);
    }

    private static long ToLong(RedisValue value) =>
        value.IsNullOrEmpty ? 0 : long.Parse(value.ToString(), CultureInfo.InvariantCulture);
}

/// <summary>Counters of the assistant (<c>oneloaf.ai.requests</c>, <c>oneloaf.ai.tokens</c>, <c>oneloaf.ai.cost.usd</c>).</summary>
public sealed class AssistantMetrics
{
    private readonly Counter<long> _requests;
    private readonly Counter<long> _tokens;
    private readonly Counter<double> _cost;

    /// <summary>Creates the counters on the OneLoaf meter.</summary>
    public AssistantMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Meters.OneLoaf);
        _requests = meter.CreateCounter<long>("oneloaf.ai.requests", description: "Assistant turns sent to Claude.");
        _tokens = meter.CreateCounter<long>("oneloaf.ai.tokens", description: "Tokens billed by Claude.");
        _cost = meter.CreateCounter<double>("oneloaf.ai.cost.usd", description: "Cost of the assistant by the configured prices.");
    }

    /// <summary>A finished turn.</summary>
    public void Turn(long tokens, decimal costUsd)
    {
        _requests.Add(1);
        _tokens.Add(tokens);
        _cost.Add((double)costUsd);
    }
}

/// <summary>Readiness check: Redis answers PING.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}

/// <summary>Registration of the Assistant infrastructure. Called only from <c>Program.cs</c>.</summary>
public static class DependencyInjection
{
    /// <summary>Name of the connection string.</summary>
    public const string ConnectionStringName = "OneLoaf";

    /// <summary>Name of the Core API HTTP client.</summary>
    public const string CoreApiClientName = "core-api";

    /// <summary>Registers Claude, the Core API client, the database, Redis and the readiness checks.</summary>
    public static IServiceCollection AddAssistantInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not set (env ConnectionStrings__{ConnectionStringName} or user-secrets).");
        }

        services.AddOneLoafDatabase(connectionString);
        services.AddScoped<IConversationRepository, ConversationRepository>();

        services.AddValidatedOptions<AssistantOptions>(AssistantOptions.SectionName);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AssistantOptions>>().Value);
        services.AddValidatedOptions<AnthropicOptions>(AnthropicOptions.SectionName);
        services.AddSingleton<IClaudeClient, AnthropicClaudeClient>();

        services.AddValidatedOptions<CoreApiOptions>(CoreApiOptions.SectionName);
        services.AddHttpClient<ICoreApiClient, CoreApiClient>((sp, http) =>
                http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<CoreApiOptions>>().Value.CoreApiBaseUrl))
            .AddCorrelationIdPropagation()
            .AddStandardResilienceHandler();

        services.AddValidatedOptions<RedisOptions>(RedisOptions.SectionName);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = ConfigurationOptions.Parse(sp.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<IPendingActionStore, RedisPendingActionStore>();
        services.AddSingleton<AssistantMetrics>();
        services.AddSingleton<IUsageStore, RedisUsageStore>();

        services.AddHealthChecks()
            .AddDbContextCheck<OneLoafDbContext>("postgres", tags: [HealthTags.Ready])
            .AddCheck<RedisHealthCheck>("redis", tags: [HealthTags.Ready]);
        return services;
    }
}
