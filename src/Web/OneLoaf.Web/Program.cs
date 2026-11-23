using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OneLoaf.Web;
using OneLoaf.Web.Account;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Device;
using OneLoaf.Web.Shared.State;

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
services.AddScoped<ISettingsService, SettingsService>();

services.AddRecipesFeature()
    .AddProductionFeature();

// History, Assistant and Library are loaded on first navigation (App.razor); their Fluxor features are added to the
// store then, and their states are resolved through LazyState (ADR-F04).
services.AddScoped<LazyFeatureRegistry>();

services.AddFluxor(options => options.ScanAssemblies(typeof(App).Assembly, [.. FeatureAssemblies.Shared]));

// After AddFluxor: Fluxor maps IState<> as an open generic too, and the last open-generic registration wins.
// LazyState finds any feature in the store, so it also serves the states found by the startup scan.
services.AddScoped(typeof(IState<>), typeof(LazyState<>));

var host = builder.Build();

// Restore the token of this tab before the first render, so AuthorizeRouteView sees the session at once.
await host.Services.GetRequiredService<TokenStore>().InitializeAsync();
await host.RunAsync();
