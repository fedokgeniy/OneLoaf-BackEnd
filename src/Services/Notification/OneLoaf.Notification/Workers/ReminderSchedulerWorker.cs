using Microsoft.Extensions.Options;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Application.Reminders;

namespace OneLoaf.Notification.Workers;

/// <summary>Runs the reminder scheduler every <c>Notification__SchedulerIntervalSeconds</c>.</summary>
public sealed partial class ReminderSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    TimeProvider timeProvider,
    ILogger<ReminderSchedulerWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SchedulerIntervalSeconds), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ReminderScheduler>().RunOnceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRunFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Reminder scheduler run failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
