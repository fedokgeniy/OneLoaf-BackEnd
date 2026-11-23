using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneLoaf.Infrastructure;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.Messaging.RabbitMq;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Application.Events;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Options;
using StackExchange.Redis;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>Registration of the Notification infrastructure. Called only from <c>Program.cs</c>.</summary>
public static class DependencyInjection
{
    /// <summary>Name of the connection string.</summary>
    public const string ConnectionStringName = "OneLoaf";

    /// <summary>Registers the database, Redis, the channels, RabbitMQ publisher and consumers, and the readiness checks.</summary>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not set (env ConnectionStrings__{ConnectionStringName} or user-secrets).");
        }

        services.AddOneLoafDatabase(connectionString);
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IRecipientDirectory, RecipientDirectory>();

        services.AddValidatedOptions<NotificationOptions>(NotificationOptions.SectionName);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NotificationOptions>>().Value);
        services.AddValidatedOptions<SmtpOptions>(SmtpOptions.SectionName);
        services.AddHttpClient<PushServiceClient>().AddCorrelationIdPropagation();
        services.AddScoped<INotificationChannel, WebPushChannel>();
        services.AddScoped<INotificationChannel, EmailChannel>();

        services.AddValidatedOptions<RedisOptions>(RedisOptions.SectionName);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = ConfigurationOptions.Parse(sp.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<IReminderInFlightMarker, RedisReminderInFlightMarker>();

        services.AddRabbitMqPublisher();
        services.AddRabbitMqConsumer(QueueNames.NotificationReminders, (queue, s) => queue
            .On<ReminderDue, ReminderDispatcher>(RoutingKeys.ReminderDue, s));
        services.AddRabbitMqConsumer(QueueNames.NotificationBatchEvents, (queue, s) => queue
            .On<BatchPlanned, BatchPlannedConsumer>(RoutingKeys.BatchPlanned, s)
            .On<BatchCompleted, BatchCompletedConsumer>(RoutingKeys.BatchCompleted, s));
        services.AddRabbitMqConsumer(QueueNames.NotificationStarterEvents, (queue, s) => queue
            .On<StarterFed, StarterFedConsumer>(RoutingKeys.StarterFed, s));

        services.AddHealthChecks()
            .AddDbContextCheck<OneLoafDbContext>("postgres", tags: [HealthTags.Ready])
            .AddCheck<RedisHealthCheck>("redis", tags: [HealthTags.Ready]);
        return services;
    }
}
