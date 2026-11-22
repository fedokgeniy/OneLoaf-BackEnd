using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.ServiceDefaults.Errors;

/// <summary>
/// Maps exceptions to HTTP responses as specified in api-design.md §4 and integrity-and-transactions.md §5.
/// Pure function: unit tested for every row of the table.
/// </summary>
public static class ExceptionMapper
{
    /// <summary>Non-standard status for a request cancelled by the client (nginx convention); no body is written.</summary>
    public const int ClientClosedRequest = 499;

    internal const string ValidationTitle = "One or more validation errors occurred.";
    internal const string BadRequestTitle = "The request is invalid.";
    internal const string ForbiddenTitle = "You are not allowed to perform this action.";
    internal const string NotFoundTitle = "The requested resource was not found.";
    internal const string ConflictTitle = "The request conflicts with existing data.";
    internal const string StateConflictTitle = "The action is not allowed in the current state.";
    internal const string BusinessRuleTitle = "The request violates a business rule.";
    internal const string UnavailableTitle = "A required service is temporarily unavailable.";
    internal const string InternalTitle = "An unexpected error occurred.";
    internal const string CancelledTitle = "The request was cancelled.";

    /// <summary>Maps <paramref name="exception"/> to a response.</summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="requestAborted">True when the request token is cancelled (the client went away).</param>
    public static ProblemMapping Map(Exception exception, bool requestAborted)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && requestAborted)
        {
            return new ProblemMapping(ClientClosedRequest, ProblemTypes.BadRequest, CancelledTitle, null,
                LogLevel.Debug, WriteBody: false);
        }

        return exception switch
        {
            ValidationException validation => new ProblemMapping(StatusCodes.Status400BadRequest,
                ProblemTypes.Validation, ValidationTitle, null, LogLevel.Information, Errors: ToErrors(validation)),
            BadHttpRequestException badRequest => new ProblemMapping(badRequest.StatusCode,
                ProblemTypes.BadRequest, BadRequestTitle, null, LogLevel.Information),
            NotFoundException e => ClientError(StatusCodes.Status404NotFound, ProblemTypes.NotFound, NotFoundTitle, e),
            ConflictException e => ClientError(StatusCodes.Status409Conflict, ProblemTypes.Conflict, ConflictTitle, e),
            BusinessRuleException e => ClientError(StatusCodes.Status422UnprocessableEntity, ProblemTypes.BusinessRule,
                BusinessRuleTitle, e),
            ForbiddenException e => ClientError(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden, ForbiddenTitle, e),
            ExternalServiceUnavailableException e => new ProblemMapping(StatusCodes.Status503ServiceUnavailable,
                ProblemTypes.ServiceUnavailable, UnavailableTitle, e.Message, LogLevel.Warning, RetryAfter: e.RetryAfter),
            _ when FindPostgresException(exception) is { } postgres => MapPostgres(postgres),
            _ => Internal(),
        };
    }

    /// <summary>
    /// Finds a <see cref="PostgresException"/> in the exception or its inner exceptions. This covers EF Core's
    /// <c>DbUpdateException</c> without referencing EF Core here.
    /// </summary>
    internal static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    /// <summary>
    /// SQLSTATE → HTTP. The database message is not returned: it may contain constraint names or values of other
    /// rows. Friendly per-constraint titles can be added later by constraint name (api-design.md §4).
    /// </summary>
    private static ProblemMapping MapPostgres(PostgresException postgres)
    {
        var sqlState = postgres.SqlState;
        return sqlState switch
        {
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation =>
                Db(StatusCodes.Status409Conflict, ProblemTypes.Conflict, ConflictTitle, sqlState),
            PostgresErrorCodes.ObjectNotInPrerequisiteState =>
                Db(StatusCodes.Status409Conflict, ProblemTypes.Conflict, StateConflictTitle, sqlState),
            PostgresErrorCodes.CheckViolation =>
                Db(StatusCodes.Status422UnprocessableEntity, ProblemTypes.BusinessRule, BusinessRuleTitle, sqlState),
            PostgresErrorCodes.IntegrityConstraintViolation =>
                Db(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden, ForbiddenTitle, sqlState),
            PostgresErrorCodes.InvalidParameterValue =>
                Db(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest, BadRequestTitle, sqlState),
            PostgresErrorCodes.NoDataFound =>
                Db(StatusCodes.Status404NotFound, ProblemTypes.NotFound, NotFoundTitle, sqlState),
            _ => Internal() with { SqlState = sqlState },
        };
    }

    private static ProblemMapping ClientError(int status, string type, string title, Exception exception) =>
        new(status, type, title, exception.Message, LogLevel.Information);

    private static ProblemMapping Db(int status, string type, string title, string sqlState) =>
        new(status, type, title, null, LogLevel.Information, SqlState: sqlState);

    private static ProblemMapping Internal() =>
        new(StatusCodes.Status500InternalServerError, ProblemTypes.Internal, InternalTitle, null, LogLevel.Error);

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName ?? string.Empty), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
}
