namespace OneLoaf.Web.Shared.Ui;

/// <summary>Unique element ids for label/aria wiring (the WebAssembly runtime is single-threaded).</summary>
internal static class ElementIds
{
    private static int _next;

    /// <summary>Returns a new id such as <c>ol-field-7</c>.</summary>
    public static string Next(string prefix) => $"ol-{prefix}-{Interlocked.Increment(ref _next)}";
}
