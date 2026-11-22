namespace OneLoaf.ServiceDefaults.Security;

/// <summary>Names of authorization policies. The fallback policy (any endpoint without one) requires an authenticated user.</summary>
public static class Policies
{
    /// <summary>Only administrators (<c>/admin/*</c>).</summary>
    public const string Admin = "admin";
}
