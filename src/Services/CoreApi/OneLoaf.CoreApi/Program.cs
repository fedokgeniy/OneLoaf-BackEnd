using OneLoaf.CoreApi.Infrastructure;
using OneLoaf.ServiceDefaults;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Health;

// Docker HEALTHCHECK: the same binary probes /health/live and exits.
if (HealthProbe.IsRequested(args))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(ServiceNames.CoreApi);
builder.Services.AddCoreApiInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
app.MapServiceDefaults();

await app.RunAsync();
return 0;

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
