using OneLoaf.Notification.Application;
using OneLoaf.Notification.Endpoints;
using OneLoaf.Notification.Infrastructure;
using OneLoaf.Notification.Workers;
using OneLoaf.ServiceDefaults;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Health;

// Docker HEALTHCHECK: the same binary probes /health/live and exits.
if (HealthProbe.IsRequested(args))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(ServiceNames.Notification);
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReminderSchedulerWorker>();

var app = builder.Build();

app.UseServiceDefaults();
app.MapServiceDefaults();
app.MapNotificationEndpoints();

await app.RunAsync();
return 0;

/// <summary>Kept internal so test projects that also reference the Core API see one public <c>Program</c>.</summary>
internal sealed partial class Program;
