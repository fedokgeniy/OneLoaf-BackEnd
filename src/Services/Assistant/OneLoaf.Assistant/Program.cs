using OneLoaf.Assistant.Application;
using OneLoaf.Assistant.Endpoints;
using OneLoaf.Assistant.Infrastructure;
using OneLoaf.ServiceDefaults;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Health;

// Docker HEALTHCHECK: the same binary probes /health/live and exits.
if (HealthProbe.IsRequested(args))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(ServiceNames.Assistant);
builder.Services.AddAssistantApplication();
builder.Services.AddAssistantInfrastructure(builder.Configuration);
builder.Services.AddAssistantRateLimiting();

var app = builder.Build();

app.UseServiceDefaults();
app.UseRateLimiter();

app.MapServiceDefaults();
app.MapAssistantEndpoints();

await app.RunAsync();
return 0;

/// <summary>Kept internal so test projects that also reference the Core API see one public <c>Program</c>.</summary>
internal sealed partial class Program;
