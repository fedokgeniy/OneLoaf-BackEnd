using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace OneLoaf.Web.Layout;

/// <summary>
/// Last safety net around the page body: logs a rendering exception and shows a recoverable error panel instead of
/// the Blazor error bar. <see cref="MainLayout"/> recovers it on every navigation.
/// </summary>
public sealed partial class AppErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<AppErrorBoundary> Logger { get; set; } = default!;

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        LogUnhandled(Logger, exception);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while rendering the page")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
