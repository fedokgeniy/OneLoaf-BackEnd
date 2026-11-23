using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneLoaf.Messaging.Contracts;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Options;
using RabbitMQ.Client;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>Broker settings (<c>RabbitMq__Host</c>, <c>RabbitMq__UserName</c>, <c>RabbitMq__Password</c>, ...).</summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Host name.</summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>AMQP port.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host.</summary>
    [Required]
    public string VirtualHost { get; set; } = "/";

    /// <summary>User name.</summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Topic exchange of the events.</summary>
    [Required]
    public string Exchange { get; set; } = Exchanges.Events;

    /// <summary>Publish attempts before an event is given up (logged).</summary>
    [Range(1, 10)]
    public int PublishAttempts { get; set; } = 3;

    /// <summary>Delivery attempts of a consumed message before it is dead-lettered.</summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay before a failed message is delivered again.</summary>
    [Range(1, 3600)]
    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>Unacknowledged messages a consumer receives at once.</summary>
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;
}

/// <summary>One shared broker connection for the process; channels are created per operation.</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private bool _topologyDeclared;

    /// <summary>Returns the open connection, connecting (and declaring the exchange) when needed.</summary>
    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } open && _topologyDeclared)
        {
            return open;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }

                var settings = options.Value;
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    Port = settings.Port,
                    VirtualHost = settings.VirtualHost,
                    UserName = settings.UserName,
                    Password = settings.Password,
                    ClientProvidedName = AppDomain.CurrentDomain.FriendlyName,
                };
                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _topologyDeclared = false;
            }

            if (!_topologyDeclared)
            {
                await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true,
                    autoDelete: false, cancellationToken: cancellationToken);
                _topologyDeclared = true;
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}

/// <summary>
/// Publishes events as persistent JSON messages with publisher confirms and the envelope headers (message id,
/// correlation id, traceparent, event type, occurred-at). Retries a few times; a final failure is logged, not thrown.
/// </summary>
public sealed partial class RabbitMqEventPublisher(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ICorrelationContext correlation,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    private const string JsonContentType = "application/json";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <inheritdoc />
    public async Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType(), MessagingJson.Options);
        var properties = CreateProperties(integrationEvent);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var open = await connection.GetAsync(cancellationToken);
                await using var channel = await open.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    cancellationToken);
                await channel.BasicPublishAsync(options.Value.Exchange, integrationEvent.RoutingKey, mandatory: false,
                    properties, body, cancellationToken);
                LogPublished(logger, integrationEvent.RoutingKey, properties.MessageId!);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt >= options.Value.PublishAttempts)
                {
                    LogGivenUp(logger, ex, integrationEvent.RoutingKey, attempt);
                    return;
                }

                await Task.Delay(RetryDelay * attempt, cancellationToken);
            }
        }
    }

    private BasicProperties CreateProperties(IntegrationEvent integrationEvent)
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MessageHeaders.EventType] = integrationEvent.RoutingKey,
            [MessageHeaders.OccurredAt] = integrationEvent.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
        };
        if (correlation.CorrelationId is { } correlationId)
        {
            headers[MessageHeaders.CorrelationId] = correlationId;
        }

        if (Activity.Current?.Id is { } traceParent)
        {
            headers[MessageHeaders.TraceParent] = traceParent;
        }

        return new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlation.CorrelationId,
            ContentType = JsonContentType,
            DeliveryMode = DeliveryModes.Persistent,
            Type = integrationEvent.RoutingKey,
            Timestamp = new AmqpTimestamp(integrationEvent.OccurredAt.ToUnixTimeSeconds()),
            Headers = headers,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {RoutingKey} {MessageId}")]
    private static partial void LogPublished(ILogger logger, string routingKey, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not publish {RoutingKey} after {Attempts} attempts")]
    private static partial void LogGivenUp(ILogger logger, Exception exception, string routingKey, int attempts);
}

/// <summary>Readiness check: the broker connection can be opened.</summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var open = await connection.GetAsync(cancellationToken);
            return open.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}

/// <summary>Registration of the RabbitMQ publisher.</summary>
public static class RabbitMqExtensions
{
    /// <summary>Name of the readiness check.</summary>
    public const string HealthCheckName = "rabbitmq";

    /// <summary>Registers options, the shared connection, <see cref="IEventPublisher"/> and the readiness check.</summary>
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddValidatedOptions<RabbitMqOptions>(RabbitMqOptions.SectionName);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>(HealthCheckName, tags: [HealthTags.Ready]);
        return services;
    }
}
