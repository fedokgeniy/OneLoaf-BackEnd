using OneLoaf.CoreApi.Application;
using OneLoaf.CoreApi.Endpoints;
using OneLoaf.CoreApi.Infrastructure;
using OneLoaf.CoreApi.Infrastructure.Caching;
using OneLoaf.CoreApi.RateLimiting;
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
builder.Services.AddCoreApiApplication();
builder.Services.AddCoreApiInfrastructure(builder.Configuration);
builder.Services.AddAuthRateLimiting();

var app = builder.Build();

app.UseServiceDefaults();
app.UseRateLimiter();
app.UseCalculationCacheInvalidation();

app.MapServiceDefaults();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapWorkspaceEndpoints();
app.MapCatalogEndpoints();
app.MapRecipeEndpoints();
app.MapProductionEndpoints();

await app.RunAsync();
return 0;

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
