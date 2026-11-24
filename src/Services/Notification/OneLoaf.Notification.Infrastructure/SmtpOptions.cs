using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>SMTP settings (<c>Smtp__Host</c>, <c>Smtp__Port</c>, <c>Smtp__User</c>, <c>Smtp__Password</c>, <c>Smtp__From</c>).</summary>
public sealed class SmtpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Smtp";

    /// <summary>SMTP host; e-mail is disabled when empty.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Port (587 with STARTTLS).</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>User name.</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Password; kept only in the environment.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender, e.g. <c>OneLoaf &lt;no-reply@example.com&gt;</c>.</summary>
    public string From { get; set; } = "OneLoaf <no-reply@example.com>";
}
