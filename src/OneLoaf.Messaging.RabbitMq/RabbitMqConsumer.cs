using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneLoaf.ServiceDefaults.Correlation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>
/// Consumes one queue. Declares the topology idempotently (queue bound to the events exchange, a delayed retry queue
/// whose expired messages return to the queue, the dead-letter exchange and queue), restores the correlation id, runs
/// the handler in its own DI scope and acknowledges every message: a failed message is republished to the retry queue
/// with <c>x-attempt + 1</c>, or to the dead-letter exchange after the last attempt.
/// </summary>
public sealed partial class RabbitMqConsumer(
    ConsumerRegistration registration,
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ICorrelationContext correlation,
    ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    private const string MessageTtlArgument = "x-message-ttl";
    private const string DeadLetterExchangeArgument = "x-dead-letter-exchange";
    private const string DeadLetterRoutingKeyArgument = "x-dead-letter-routing-key";
    private const string CorrelationScopeKey = "CorrelationId";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private IChannel? _channel;

    /// <summary>The consumer has declared its topology and is receiving messages.</summary>
    public bool IsReady => _channel is { IsOpen: true };

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _channel is null)
        {
            try
            {
                _channel = await OpenChannelAsync(stoppingToken);
                LogStarted(logger, registration.Queue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogConnectFailed(logger, ex, registration.Queue);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }

        // The client recovers the connection, topology and consumer itself; keep the channel until shutdown.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }

    private async Task<IChannel> OpenChannelAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var open = await connection.GetAsync(cancellationToken);
        var channel = await open.CreateChannelAsync(cancellationToken: cancellationToken);
        var queue = registration.Queue;
        var retryQueue = QueueNames.Retry(queue);

        await channel.ExchangeDeclareAsync(Exchanges.Retry, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(Exchanges.DeadLetter, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(QueueNames.DeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(QueueNames.DeadLetter, Exchanges.DeadLetter, string.Empty, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        foreach (var routingKey in registration.Routes.Keys)
        {
            await channel.QueueBindAsync(queue, settings.Exchange, routingKey, cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MessageTtlArgument] = settings.RetryDelaySeconds * 1000,
                [DeadLetterExchangeArgument] = string.Empty,
                [DeadLetterRoutingKeyArgument] = queue,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(retryQueue, Exchanges.Retry, retryQueue, cancellationToken: cancellationToken);

        await channel.BasicQosAsync(0, settings.PrefetchCount, global: false, cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery);
        await channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken);
        return channel;
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        var properties = delivery.BasicProperties;
        var eventType = properties.Type ?? ReadHeader(properties, MessageHeaders.EventType) ?? delivery.RoutingKey;
        var context = new MessageContext(
            properties.MessageId ?? delivery.DeliveryTag.ToString(CultureInfo.InvariantCulture),
            ReadAttempt(properties), options.Value.MaxAttempts,
            properties.CorrelationId ?? ReadHeader(properties, MessageHeaders.CorrelationId));
        if (context.CorrelationId is { Length: > 0 } correlationId)
        {
            correlation.SetCorrelationId(correlationId);
        }

        using var logScope = logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal) { [CorrelationScopeKey] = context.CorrelationId });
        try
        {
            if (!registration.Routes.TryGetValue(eventType, out var handle))
            {
                LogUnknownType(logger, eventType, registration.Queue);
            }
            else
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await handle(scope.ServiceProvider, delivery.Body, context, delivery.CancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !delivery.CancellationToken.IsCancellationRequested)
        {
            await RepublishAsync(channel, delivery, context, ex);
        }

        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
    }

    private async Task RepublishAsync(IChannel channel, BasicDeliverEventArgs delivery, MessageContext context, Exception error)
    {
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Headers = new Dictionary<string, object?>(delivery.BasicProperties.Headers ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
            {
                [MessageHeaders.Attempt] = context.Attempt + 1,
            },
        };

        if (context.IsLastAttempt)
        {
            LogDeadLettered(logger, error, context.MessageId, registration.Queue, context.Attempt);
            await channel.BasicPublishAsync(Exchanges.DeadLetter, string.Empty, mandatory: false, properties, delivery.Body);
            return;
        }

        LogRetry(logger, error, context.MessageId, registration.Queue, context.Attempt);
        var retryQueue = QueueNames.Retry(registration.Queue);
        await channel.BasicPublishAsync(Exchanges.Retry, retryQueue, mandatory: false, properties, delivery.Body);
    }

    private static int ReadAttempt(IReadOnlyBasicProperties properties) =>
        properties.Headers?.TryGetValue(MessageHeaders.Attempt, out var value) == true
            ? value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 1,
            }
            : 1;

    private static string? ReadHeader(IReadOnlyBasicProperties properties, string name) =>
        properties.Headers?.TryGetValue(name, out var value) == true
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => null,
            }
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Queue}")]
    private static partial void LogStarted(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start consuming {Queue}; retrying")]
    private static partial void LogConnectFailed(ILogger logger, Exception exception, string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No handler for {EventType} in {Queue}; message dropped")]
    private static partial void LogUnknownType(ILogger logger, string eventType, string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} from {Queue} failed on attempt {Attempt}; retry scheduled")]
    private static partial void LogRetry(ILogger logger, Exception exception, string messageId, string queue, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Message {MessageId} from {Queue} failed after {Attempt} attempts; dead-lettered")]
    private static partial void LogDeadLettered(ILogger logger, Exception exception, string messageId, string queue, int attempt);
}
