namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>A usage limit or budget is exhausted. Mapped to 429 with a <c>Retry-After</c> header.</summary>
public class TooManyRequestsException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "A usage limit was reached. Try again later.";

    /// <summary>Creates the exception with a default message.</summary>
    public TooManyRequestsException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public TooManyRequestsException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public TooManyRequestsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the time until the limit resets.</summary>
    public TooManyRequestsException(string message, TimeSpan retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the client should wait; null when unknown.</summary>
    public TimeSpan? RetryAfter { get; }
}
