namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>
/// A dependency (Claude API, Core API called from the Assistant) is unavailable or timed out.
/// Mapped to 503 with a <c>Retry-After</c> header.
/// </summary>
public class ExternalServiceUnavailableException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "A required service is temporarily unavailable.";

    /// <summary>Retry delay suggested to the client when none is given.</summary>
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>Creates the exception with a default message.</summary>
    public ExternalServiceUnavailableException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public ExternalServiceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public ExternalServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with a client-safe message and a suggested retry delay.</summary>
    public ExternalServiceUnavailableException(string message, TimeSpan retryAfter, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the client should wait before retrying.</summary>
    public TimeSpan RetryAfter { get; } = DefaultRetryAfter;
}
