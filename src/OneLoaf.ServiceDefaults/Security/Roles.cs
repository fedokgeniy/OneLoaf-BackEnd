namespace OneLoaf.ServiceDefaults.Security;

/// <summary>Values of the <c>role</c> claim; the same snake_case strings as <c>users.role</c> in the database.</summary>
public static class Roles
{
    /// <summary>Registered baker.</summary>
    public const string User = "user";

    /// <summary>Administrator (content moderation, LR-11).</summary>
    public const string Admin = "admin";
}
