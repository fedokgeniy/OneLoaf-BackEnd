using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OneLoaf.Domain;
using OneLoaf.Messaging.RabbitMq;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Endpoints;

namespace OneLoaf.IntegrationTests.Infrastructure;

/// <summary>The Notification service in-process; push and e-mail are replaced by <see cref="RecordingChannel"/>.</summary>
public sealed class NotificationFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<NotificationEntryPoint>
{
    /// <summary>Messages "delivered" by the service.</summary>
    public RecordingChannel Channel { get; } = new();

    /// <summary>Starts the host and waits until every RabbitMQ consumer has declared its queue and is consuming.</summary>
    public async Task WaitUntilConsumingAsync()
    {
        var consumers = Services.GetServices<IHostedService>().OfType<RabbitMqConsumer>().ToList();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!consumers.TrueForAll(c => c.IsReady))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The notification consumers did not start.");
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(CoreApiFactory.Environment);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationChannel>();
            services.AddSingleton<INotificationChannel>(Channel);
        });
    }
}

/// <summary>A push channel that records deliveries; a body containing <see cref="FailMarker"/> fails like an unreachable push service.</summary>
public sealed class RecordingChannel : INotificationChannel
{
    /// <summary>Put into a reminder message to make every delivery attempt fail.</summary>
    public const string FailMarker = "[fail]";

    /// <summary>Delivered messages with the recipient.</summary>
    public ConcurrentQueue<(Guid UserId, NotificationMessage Message)> Delivered { get; } = new();

    /// <summary>Failed delivery attempts.</summary>
    public int FailedAttempts => _failedAttempts;

    private int _failedAttempts;

    /// <inheritdoc />
    public ReminderChannel Channel => ReminderChannel.Push;

    /// <inheritdoc />
    public Task<DeliveryOutcome> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        if (message.Body.Contains(FailMarker, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _failedAttempts);
            throw new HttpRequestException("Push service unavailable (test).");
        }

        Delivered.Enqueue((recipient.UserId, message));
        return Task.FromResult(DeliveryOutcome.Delivered);
    }
}
