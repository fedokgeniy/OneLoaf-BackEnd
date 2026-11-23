using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Infrastructure.Claude;
using OneLoaf.Infrastructure;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Options;
using StackExchange.Redis;

namespace OneLoaf.Assistant.Infrastructure;

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
