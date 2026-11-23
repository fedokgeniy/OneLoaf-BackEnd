using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OneLoaf.Web;
using OneLoaf.Web.Account;
using OneLoaf.Web.Features.Assistant;
using OneLoaf.Web.Features.History;
using OneLoaf.Web.Features.Library;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Device;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Never logs tokens or bodies (LoggingHandler logs method, path, status, duration only).
builder.Logging.SetMinimumLevel(builder.HostEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);
// The framework's HttpClient logging writes full URLs with query strings; LoggingHandler covers requests instead.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

var services = builder.Services;
services.AddOneLoafApi(builder.Configuration, new Uri(builder.HostEnvironment.BaseAddress));

services.AddAuthorizationCore();
services.AddScoped<JwtAuthenticationStateProvider>();
services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
services.AddScoped<SessionMonitor>();
services.AddScoped<DeviceContext>();
services.AddScoped<AccountApi>();

services.AddRecipesFeature()
    .AddProductionFeature()
    .AddHistoryFeature()
    .AddAssistantFeature()
    .AddLibraryFeature();

services.AddFluxor(options => options.ScanAssemblies(typeof(App).Assembly, [.. FeatureAssemblies.Shared]));

var host = builder.Build();

// Restore the token of this tab before the first render, so AuthorizeRouteView sees the session at once.
await host.Services.GetRequiredService<TokenStore>().InitializeAsync();
await host.RunAsync();
