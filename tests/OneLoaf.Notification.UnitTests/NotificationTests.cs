using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Application.Events;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Notification.UnitTests;

public sealed class ReminderDispatcherTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scheduled_IsDeliveredAndMarkedSent()
    {
        var (dispatcher, reminders, channel) = Create();

        await dispatcher.HandleAsync(Due(reminders.Add()), Context(1), Ct);

        Assert.Equal(ReminderStatus.Sent, reminders.Single.Status);
        Assert.Equal("Kneading starts soon", Assert.Single(channel.Sent).Title);
    }

    [Fact]
    public async Task AlreadySent_IsIgnored()
    {
        var (dispatcher, reminders, channel) = Create();
        var id = reminders.Add(ReminderStatus.Sent);

        await dispatcher.HandleAsync(Due(id), Context(1), Ct);

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task TransientFailure_CountsAttemptAndThrowsForRetry()
    {
        var (dispatcher, reminders, channel) = Create();
        channel.Fail = true;
        var id = reminders.Add();

        await Assert.ThrowsAsync<HttpRequestException>(() => dispatcher.HandleAsync(Due(id), Context(1), Ct));

        Assert.Equal(ReminderStatus.Scheduled, reminders.Single.Status);
        Assert.Equal(1, reminders.Single.Attempts);
    }

    [Fact]
    public async Task FailureOnLastAttempt_MarksFailedAndThrowsForDeadLetter()
    {
        var (dispatcher, reminders, channel) = Create();
        channel.Fail = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => dispatcher.HandleAsync(Due(reminders.Add()), Context(5), Ct));

        Assert.Equal(ReminderStatus.Failed, reminders.Single.Status);
    }

    [Fact]
    public async Task NoDevice_MarksFailed()
    {
        var (dispatcher, reminders, channel) = Create();
        channel.Outcome = DeliveryOutcome.NoTarget;

        await dispatcher.HandleAsync(Due(reminders.Add()), Context(1), Ct);

        Assert.Equal(ReminderStatus.Failed, reminders.Single.Status);
    }

    private static (ReminderDispatcher, FakeReminders, FakeChannel) Create()
    {
        var reminders = new FakeReminders(Anna);
        var channel = new FakeChannel();
        var dispatcher = new ReminderDispatcher(reminders, new FakeRecipients(), [channel], new NotificationMetrics(new FakeMeterFactory()),
            TimeProvider.System, NullLogger<ReminderDispatcher>.Instance);
        return (dispatcher, reminders, channel);
    }

    private static ReminderDue Due(Guid id) => new(id, Anna, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static MessageContext Context(int attempt) => new("m1", attempt, 5, "c1");
}

public sealed class ReminderSchedulerTests
{
    [Fact]
    public async Task RunOnce_PublishesEachDueReminderOnlyOnce()
    {
        var reminders = new FakeReminders(Guid.NewGuid());
        reminders.Add();
        reminders.Add();
        var publisher = new FakePublisher();
        var scheduler = new ReminderScheduler(reminders, new FakeMarker(), publisher, TimeProvider.System, new NotificationOptions(),
            NullLogger<ReminderScheduler>.Instance);

        var first = await scheduler.RunOnceAsync(TestContext.Current.CancellationToken);
        var second = await scheduler.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, publisher.Published.Count);
    }
}

public sealed class TemplateAndPushTests
{
    [Fact]
    public void BatchPlanned_ShowsFirstReminderInKitchenTimeZone()
    {
        var e = new BatchPlanned(Guid.NewGuid(), Guid.NewGuid(), "Weekend", DateTimeOffset.UtcNow, null, null,
            new DateTimeOffset(2026, 12, 14, 4, 30, 0, TimeSpan.Zero), DateTimeOffset.UtcNow);

        var message = EventTemplates.BatchPlanned(e, new Recipient(e.OwnerId, "a@example.com", "Europe/Vilnius"));

        Assert.Equal("\"Weekend\" is planned. First reminder at 06:30, 14 Dec.", message.Body);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", "BNcR_x-1", "tBHI", true)]
    [InlineData("http://insecure.example", "BNcR", "tBHI", false)]
    [InlineData("https://push.example", "not base64!", "tBHI", false)]
    public void SubscribeValidator_ChecksEndpointAndKeys(string endpoint, string p256dh, string auth, bool valid) =>
        Assert.Equal(valid, new SubscribeRequestValidator().Validate(new SubscribeRequest(endpoint, new PushKeys(p256dh, auth))).IsValid);

    [Fact]
    public async Task TestPush_WithoutDevices_IsBusinessRuleViolation()
    {
        var channel = new FakeChannel { Outcome = DeliveryOutcome.NoTarget };
        var service = new PushService(new FakeSubscriptions(), new FakeRecipients(), [channel], new NotificationOptions(), new FakeUser());

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SendTestAsync(TestContext.Current.CancellationToken));
        Assert.Throws<ExternalServiceUnavailableException>(service.GetVapidPublicKey);
    }
}

internal sealed class FakeReminders(Guid owner) : IReminderRepository
{
    private readonly List<Row> _rows = [];

    public Row Single => _rows.Single();

    public Guid Add(ReminderStatus status = ReminderStatus.Scheduled)
    {
        var row = new Row { Id = Guid.NewGuid(), Status = status };
        _rows.Add(row);
        return row.Id;
    }

    public async Task<int> ProcessDueAsync(DateTimeOffset now, int limit, Func<DueReminder, CancellationToken, Task<bool>> process, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var row in _rows.Where(r => r.Status == ReminderStatus.Scheduled).Take(limit).ToList())
        {
            count += await process(new DueReminder(row.Id, owner, now), cancellationToken) ? 1 : 0;
        }

        return count;
    }

    public Task<ReminderDetails?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.Where(r => r.Id == id).Select(r => new ReminderDetails(r.Id, owner, ReminderKind.BeforeKneading,
            ReminderChannel.Push, r.Status, "Knead in 60 minutes.", DateTimeOffset.UtcNow, Guid.NewGuid(), null)).FirstOrDefault());

    public Task<bool> MarkSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        var row = _rows.Single(r => r.Id == id);
        row.Status = ReminderStatus.Sent;
        row.Attempts++;
        return Task.FromResult(true);
    }

    public Task MarkAttemptFailedAsync(Guid id, bool final, CancellationToken cancellationToken)
    {
        var row = _rows.Single(r => r.Id == id);
        row.Attempts++;
        row.Status = final ? ReminderStatus.Failed : row.Status;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReminderDto>> ListAsync(Guid ownerId, ReminderListQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    internal sealed class Row
    {
        public Guid Id { get; init; }

        public ReminderStatus Status { get; set; }

        public int Attempts { get; set; }
    }
}

internal sealed class FakeChannel : INotificationChannel
{
    public List<NotificationMessage> Sent { get; } = [];

    public bool Fail { get; set; }

    public DeliveryOutcome Outcome { get; set; } = DeliveryOutcome.Delivered;

    public ReminderChannel Channel => ReminderChannel.Push;

    public Task<DeliveryOutcome> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new HttpRequestException("down");
        }

        if (Outcome == DeliveryOutcome.Delivered)
        {
            Sent.Add(message);
        }

        return Task.FromResult(Outcome);
    }
}

internal sealed class FakeRecipients : IRecipientDirectory
{
    public Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<Recipient?>(new Recipient(userId, "baker@example.com", "Europe/Vilnius"));
}

internal sealed class FakeMarker : IReminderInFlightMarker
{
    private readonly HashSet<Guid> _marked = [];

    public Task<bool> TryMarkAsync(Guid reminderId, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromResult(_marked.Add(reminderId));
}

internal sealed class FakePublisher : IEventPublisher
{
    public List<IntegrationEvent> Published { get; } = [];

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSubscriptions : IPushSubscriptionRepository
{
    public Task<(PushSubscriptionDto Subscription, bool Created)> UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IReadOnlyList<PushTarget>> ListTargetsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PushTarget>>([]);

    public Task DeleteExpiredAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeUser : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid Id { get; } = Guid.NewGuid();

    public string? Role => null;

    public bool IsAdmin => false;
}

internal sealed class FakeMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}
