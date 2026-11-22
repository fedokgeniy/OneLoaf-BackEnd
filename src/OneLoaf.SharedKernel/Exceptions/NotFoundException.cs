namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>The resource does not exist or belongs to another user (US-03). Mapped to 404.</summary>
public class NotFoundException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "The requested resource was not found.";

    /// <summary>Creates the exception with a default message.</summary>
    public NotFoundException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
