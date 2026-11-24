using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>
/// Serves the published web client (<c>dotnet publish</c> output, <c>wwwroot</c>) on a free local port with the SPA
/// fallback, and starts headless Chromium. Set <c>ONELOAF_E2E_WEB_ROOT</c> to the published <c>wwwroot</c>; without it
/// the journeys are skipped (e.g. in the backend workflows, which do not publish the client).
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    /// <summary>Environment variable with the path of the published wwwroot.</summary>
    public const string WebRootVariable = "ONELOAF_E2E_WEB_ROOT";

    private WebApplication? _app;
    private IPlaywright? _playwright;

    /// <summary>Path of the published app, or <see langword="null"/> when e2e tests are not configured.</summary>
    public static string? WebRoot => Environment.GetEnvironmentVariable(WebRootVariable) is { Length: > 0 } root
        && File.Exists(Path.Combine(root, "index.html")) ? root : null;

    /// <summary>Base address of the served app, e.g. <c>http://127.0.0.1:51234/</c>.</summary>
    public Uri BaseAddress { get; private set; } = new("http://127.0.0.1/");

    /// <summary>The browser shared by the tests of a class.</summary>
    public IBrowser Browser { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (WebRoot is not { } root)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { WebRootPath = root });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        var files = new PhysicalFileProvider(root);
        _app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        _app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
        _app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        BaseAddress = new Uri(address.TrimEnd('/') + "/");

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
