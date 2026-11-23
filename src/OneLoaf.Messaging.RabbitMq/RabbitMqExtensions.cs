using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Options;

namespace OneLoaf.Messaging.RabbitMq;

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
