namespace OneLoaf.Web.Layout;

/// <summary>The <c>returnUrl</c> of the sign-in page: only local paths are followed (no open redirect).</summary>
public static class ReturnUrl
{
    /// <summary>Route of the sign-in page.</summary>
    public const string LoginPath = "login";

    /// <summary>Returns <paramref name="url"/> when it is a local path such as <c>/recipes/1</c>, otherwise <c>/</c>.</summary>
    public static string Sanitize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url[0] != '/' || url.StartsWith("//", StringComparison.Ordinal)
            || url.StartsWith("/\\", StringComparison.Ordinal) || url.Contains(':', StringComparison.Ordinal)
            || IsAuthPage(url))
        {
            return "/";
        }

        return url;
    }

    /// <summary>Sign-in URL that returns to <paramref name="current"/> afterwards.</summary>
    public static string LoginUrl(string? current)
    {
        var target = Sanitize(current);
        return target == "/" ? LoginPath : $"{LoginPath}?returnUrl={Uri.EscapeDataString(target)}";
    }

    private static bool IsAuthPage(string url) =>
        url.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("/register", StringComparison.OrdinalIgnoreCase);
}
