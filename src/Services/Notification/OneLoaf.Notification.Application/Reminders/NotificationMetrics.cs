using System.Diagnostics.Metrics;
using OneLoaf.Domain;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>Counters of reminder delivery (Prometheus via OpenTelemetry).</summary>
public sealed class NotificationMetrics
{
    /// <summary>Meter name; registered by the service defaults.</summary>
    public const string MeterName = "OneLoaf";

    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;

    /// <summary>Creates the counters.</summary>
    public NotificationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _sent = meter.CreateCounter<long>("oneloaf.reminders.sent", description: "Reminders delivered.");
        _failed = meter.CreateCounter<long>("oneloaf.reminders.failed", description: "Reminders that could not be delivered.");
    }

    /// <summary>A reminder was delivered.</summary>
    public void Sent(ReminderChannel channel) => _sent.Add(1, new KeyValuePair<string, object?>("channel", channel.ToString()));

    /// <summary>A reminder failed for good.</summary>
    public void Failed(ReminderChannel channel, string reason) =>
        _failed.Add(1, new KeyValuePair<string, object?>("channel", channel.ToString()), new KeyValuePair<string, object?>("reason", reason));
}
