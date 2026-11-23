using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>Routes of one queue: event type (routing key) to the handler invocation.</summary>
public sealed class ConsumerRegistration
{
    private readonly Dictionary<string, Func<IServiceProvider, ReadOnlyMemory<byte>, MessageContext, CancellationToken, Task>> _routes =
        new(StringComparer.Ordinal);

    internal ConsumerRegistration(string queue) => Queue = queue;

    /// <summary>Queue name.</summary>
    public string Queue { get; }

    internal IReadOnlyDictionary<string, Func<IServiceProvider, ReadOnlyMemory<byte>, MessageContext, CancellationToken, Task>> Routes => _routes;

    /// <summary>Binds the queue to <paramref name="routingKey"/> and handles those events with <typeparamref name="THandler"/>.</summary>
    public ConsumerRegistration On<TEvent, THandler>(string routingKey, IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventConsumer<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<THandler>();
        _routes[routingKey] = (provider, body, context, ct) =>
        {
            var integrationEvent = JsonSerializer.Deserialize<TEvent>(body.Span, MessagingJson.Options)
                                   ?? throw new JsonException($"Empty {typeof(TEvent).Name} message.");
            return provider.GetRequiredService<THandler>().HandleAsync(integrationEvent, context, ct);
        };
        return this;
    }
}
