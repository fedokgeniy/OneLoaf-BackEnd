namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>The current user is authenticated but not allowed to perform the action. Mapped to 403.</summary>
public class ForbiddenException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "You are not allowed to perform this action.";

    /// <summary>Creates the exception with a default message.</summary>
    public ForbiddenException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public ForbiddenException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
