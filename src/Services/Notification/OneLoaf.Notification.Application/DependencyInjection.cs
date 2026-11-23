using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Notification.Application.Events;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;

namespace OneLoaf.Notification.Application;

/// <summary>Registration of the Notification use cases.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the services and validators (event consumers are registered with their queues).</summary>
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddSingleton<NotificationMetrics>();
        services.AddScoped<ReminderScheduler>();
        services.AddScoped<ReminderQueryService>();
        services.AddScoped<PushService>();
        services.AddScoped<EventNotifier>();
        services.AddValidatorsFromAssemblyContaining<SubscribeRequestValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
