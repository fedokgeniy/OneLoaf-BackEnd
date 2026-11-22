namespace OneLoaf.SharedKernel.Exceptions;

/// <summary>A business rule is violated (for example UC-02 A1: no feedback to analyse). Mapped to 422.</summary>
public class BusinessRuleException : Exception
{
    /// <summary>Client-safe message used when none is given.</summary>
    public const string DefaultMessage = "The request violates a business rule.";

    /// <summary>Creates the exception with a default message.</summary>
    public BusinessRuleException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that is safe to show to the client.</summary>
    public BusinessRuleException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a client-safe message and the underlying cause.</summary>
    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
