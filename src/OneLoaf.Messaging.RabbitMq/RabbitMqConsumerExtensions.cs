using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>Registration of the consumers.</summary>
public static class RabbitMqConsumerExtensions
{
    /// <summary>
    /// Consumes <paramref name="queue"/> in a hosted service; <paramref name="configure"/> binds routing keys to handlers.
    /// Requires <see cref="RabbitMqExtensions.AddRabbitMqPublisher"/> (connection and options).
    /// </summary>
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services, string queue, Action<ConsumerRegistration, IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var registration = new ConsumerRegistration(queue);
        configure(registration, services);
        services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<RabbitMqConsumer>(sp, registration));
        return services;
    }
}
