namespace OneLoaf.CoreApi.Application.Common;

/// <summary>Input limits, aligned with the column sizes and CHECK constraints of the database.</summary>
public static class ValidationLimits
{
    /// <summary><c>users.email</c> is <c>varchar(256)</c>.</summary>
    public const int EmailMaxLength = 256;

    /// <summary>Same pattern as <c>ck_users_email_format</c>, so a valid request never fails in the database.</summary>
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    /// <summary>Minimum password length.</summary>
    public const int PasswordMinLength = 8;

    /// <summary>Upper bound that keeps hashing cost predictable.</summary>
    public const int PasswordMaxLength = 128;

    /// <summary><c>users.display_name</c> is <c>varchar(100)</c>.</summary>
    public const int DisplayNameMaxLength = 100;
}
