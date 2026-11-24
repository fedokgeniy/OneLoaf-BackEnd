using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneLoaf.Messaging.Contracts;
using OneLoaf.ServiceDefaults.Correlation;
using RabbitMQ.Client;

namespace OneLoaf.Messaging.RabbitMq;

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
