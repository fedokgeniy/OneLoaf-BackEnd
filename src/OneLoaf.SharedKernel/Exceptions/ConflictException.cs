namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>Duplicate, resource in use, or an action not allowed in the current state. Mapped to 409.</summary>
public class ConflictException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "The request conflicts with the current state of the resource.";

    /// <summary>Creates the exception with a default message.</summary>
    public ConflictException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
