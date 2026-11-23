using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneLoaf.IntegrationTests.Infrastructure;
using OneLoaf.Messaging;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;

namespace OneLoaf.IntegrationTests.Notification;

/// <summary>US-29 reminder pipeline (scheduler → RabbitMQ → dispatcher → channel) and the push endpoints.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class NotificationTests(ApiFixture fixture)
{
    private static readonly Guid AnnaRyeStarter = new("00000000-0000-4000-5001-000000000001");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task US29_DueReminder_IsPublishedDeliveredAndMarkedSent()
    {
        var notification = fixture.Notification;
        await notification.WaitUntilConsumingAsync();
        var reminderId = await InsertDueReminderAsync("Feed the rye mother now.");

        await RunSchedulerAsync(notification);

        var status = await WaitForStatusAsync(reminderId, "sent");
        Assert.Equal("sent", status);
        var delivered = notification.Channel.Delivered.Single(d => d.Message.Tag == $"reminder-{reminderId:N}");
        Assert.Equal(Seed.Anna, delivered.UserId);
        Assert.Equal("Feed the rye mother now.", delivered.Message.Body);

        // A second run does not publish the reminder again.
        Assert.Equal(0, await RunSchedulerAsync(notification));
    }

    [Fact]
    public async Task US29_DeliveryFailsEveryAttempt_ReminderFailedAndMessageDeadLettered()
    {
        var notification = fixture.Notification;
        await notification.WaitUntilConsumingAsync();
        await using var broker = await fixture.ConnectToBrokerAsync();
        await using var channel = await broker.CreateChannelAsync(cancellationToken: Ct);
        var deadBefore = await channel.MessageCountAsync(QueueNames.DeadLetter, Ct);
        var reminderId = await InsertDueReminderAsync($"Stage starts {RecordingChannel.FailMarker}");

        await RunSchedulerAsync(notification);

        Assert.Equal("failed", await WaitForStatusAsync(reminderId, "failed"));
        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var attempts = new NpgsqlCommand("SELECT attempts FROM reminders WHERE id = @id", connection);
        attempts.Parameters.AddWithValue("id", reminderId);
        Assert.Equal(2, (int)(await attempts.ExecuteScalarAsync(Ct))!);
        Assert.Equal(deadBefore + 1, await WaitForAsync(() => channel.MessageCountAsync(QueueNames.DeadLetter, Ct), count => count > deadBefore));
    }

    [Fact]
    public async Task US29_PushSubscriptions_UpsertByEndpointAndOwnerIsolation()
    {
        using var anna = Authorized(Seed.Anna);
        using var boris = Authorized(Seed.Boris);
        var request = new SubscribeRequest($"https://push.example.test/{Guid.NewGuid():N}", new PushKeys("BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM", "tBHItJI5svbpez7KI4CCXg"));

        using var created = await anna.PostAsJsonAsync("/api/v1/push/subscriptions", request, CoreApiFactory.Json, Ct);
        using var again = await anna.PostAsJsonAsync("/api/v1/push/subscriptions", request, CoreApiFactory.Json, Ct);
        var subscription = await created.ReadAsync<PushSubscriptionDto>();
        using var invalid = await anna.PostAsJsonAsync("/api/v1/push/subscriptions",
            request with { Endpoint = "http://insecure.example.test" }, CoreApiFactory.Json, Ct);
        using var borisDelete = await boris.DeleteAsync($"/api/v1/push/subscriptions/{subscription.Id}", Ct);
        using var annaDelete = await anna.DeleteAsync($"/api/v1/push/subscriptions/{subscription.Id}", Ct);
        using var vapid = await fixture.Notification.CreateClient().GetAsync("/api/v1/push/vapid-public-key", Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borisDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, annaDelete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vapid.StatusCode);
    }

    [Fact]
    public async Task US03_Reminders_OnlyOwnAreListed()
    {
        var reminderId = await InsertDueReminderAsync("Anna's private reminder.");
        using var boris = Authorized(Seed.Boris);
        using var anna = Authorized(Seed.Anna);

        var borisReminders = await (await boris.GetAsync("/api/v1/reminders?limit=200", Ct)).ReadAsync<List<ReminderDto>>();
        var annaReminders = await (await anna.GetAsync("/api/v1/reminders?limit=200&from=2000-01-01T00:00:00Z", Ct)).ReadAsync<List<ReminderDto>>();

        Assert.DoesNotContain(borisReminders, r => r.Id == reminderId);
        Assert.Contains(annaReminders, r => r.Id == reminderId);
    }

    private HttpClient Authorized(Guid userId)
    {
        using var core = fixture.CreateClientAs(userId);
        var client = fixture.Notification.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", core.DefaultRequestHeaders.Authorization!.Parameter);
        return client;
    }

    private static async Task<int> RunSchedulerAsync(NotificationFactory notification)
    {
        await using var scope = notification.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderScheduler>().RunOnceAsync(Ct);
    }

    private async Task<Guid> InsertDueReminderAsync(string message)
    {
        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO reminders (owner_id, kind, starter_id, due_at, channel, status, message, attempts)
            VALUES (@owner, 'stage_start', @starter, now() - interval '1 minute', 'push', 'scheduled', @message, 0)
            RETURNING id
            """, connection);
        insert.Parameters.AddWithValue("owner", Seed.Anna);
        insert.Parameters.AddWithValue("starter", AnnaRyeStarter);
        insert.Parameters.AddWithValue("message", message);
        return (Guid)(await insert.ExecuteScalarAsync(Ct))!;
    }

    private async Task<string> WaitForStatusAsync(Guid reminderId, string expected) =>
        await WaitForAsync(async () =>
        {
            await using var connection = await fixture.OpenAppConnectionAsync();
            await using var select = new NpgsqlCommand("SELECT status FROM reminders WHERE id = @id", connection);
            select.Parameters.AddWithValue("id", reminderId);
            return (string)(await select.ExecuteScalarAsync(Ct))!;
        }, status => status == expected);

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var value = await read();
            if (done(value) || DateTime.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(200, Ct);
        }
    }
}
