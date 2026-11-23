using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.ServiceDefaults;

/// <summary>One test per row of the exception mapping table.</summary>
public sealed class ExceptionMapperTests
{
    public static TheoryData<string, int, string> SqlStateRows => new()
    {
        { PostgresErrorCodes.UniqueViolation, StatusCodes.Status409Conflict, ProblemTypes.Conflict },
        { PostgresErrorCodes.ForeignKeyViolation, StatusCodes.Status409Conflict, ProblemTypes.Conflict },
        { PostgresErrorCodes.CheckViolation, StatusCodes.Status422UnprocessableEntity, ProblemTypes.BusinessRule },
        { PostgresErrorCodes.IntegrityConstraintViolation, StatusCodes.Status403Forbidden, ProblemTypes.Forbidden },
        { PostgresErrorCodes.InvalidParameterValue, StatusCodes.Status400BadRequest, ProblemTypes.BadRequest },
        { PostgresErrorCodes.NoDataFound, StatusCodes.Status404NotFound, ProblemTypes.NotFound },
        { PostgresErrorCodes.ObjectNotInPrerequisiteState, StatusCodes.Status409Conflict, ProblemTypes.Conflict },
        { PostgresErrorCodes.InsufficientPrivilege, StatusCodes.Status500InternalServerError, ProblemTypes.Internal },
        { PostgresErrorCodes.SyntaxError, StatusCodes.Status500InternalServerError, ProblemTypes.Internal },
    };

    [Fact]
    public void Map_FluentValidationException_Returns400WithCamelCaseErrors()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Loaves", "Must be between 1 and 1000."),
            new ValidationFailure("Loaves", "Must be between 1 and 1000."),
            new ValidationFailure("AsOf", "Must not be in the future."),
        ]);

        var mapping = ExceptionMapper.Map(exception, requestAborted: false);

        Assert.Equal(StatusCodes.Status400BadRequest, mapping.StatusCode);
        Assert.Equal(ProblemTypes.Validation, mapping.Type);
        Assert.NotNull(mapping.Errors);
        Assert.Equal(["Must be between 1 and 1000."], mapping.Errors["loaves"]);
        Assert.Equal(["Must not be in the future."], mapping.Errors["asOf"]);
    }

    [Fact]
    public void Map_NotFoundException_Returns404WithClientMessage()
    {
        var mapping = ExceptionMapper.Map(new NotFoundException("Recipe not found."), requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status404NotFound, ProblemTypes.NotFound, LogLevel.Information);
        Assert.Equal("Recipe not found.", mapping.Detail);
    }

    [Fact]
    public void Map_ConflictException_Returns409()
    {
        var mapping = ExceptionMapper.Map(new ConflictException("Price for this date exists."), requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status409Conflict, ProblemTypes.Conflict, LogLevel.Information);
        Assert.Equal("Price for this date exists.", mapping.Detail);
    }

    [Fact]
    public void Map_BusinessRuleException_Returns422()
    {
        var mapping = ExceptionMapper.Map(new BusinessRuleException("Add feedback first."), requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status422UnprocessableEntity, ProblemTypes.BusinessRule, LogLevel.Information);
        Assert.Equal("Add feedback first.", mapping.Detail);
    }

    [Fact]
    public void Map_ForbiddenException_Returns403()
    {
        var mapping = ExceptionMapper.Map(new ForbiddenException(), requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status403Forbidden, ProblemTypes.Forbidden, LogLevel.Information);
        Assert.Equal(ForbiddenException.DefaultMessage, mapping.Detail);
    }

    [Fact]
    public void Map_ExternalServiceUnavailableException_Returns503WithRetryAfter()
    {
        var exception = new ExternalServiceUnavailableException("Assistant is unavailable.", TimeSpan.FromSeconds(15));

        var mapping = ExceptionMapper.Map(exception, requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status503ServiceUnavailable, ProblemTypes.ServiceUnavailable, LogLevel.Warning);
        Assert.Equal(TimeSpan.FromSeconds(15), mapping.RetryAfter);
    }

    [Fact]
    public void Map_ExternalServiceUnavailableExceptionWithoutDelay_UsesDefaultRetryAfter()
    {
        var mapping = ExceptionMapper.Map(new ExternalServiceUnavailableException(), requestAborted: false);

        Assert.Equal(ExternalServiceUnavailableException.DefaultRetryAfter, mapping.RetryAfter);
    }

    [Theory]
    [MemberData(nameof(SqlStateRows))]
    public void Map_PostgresException_MapsSqlStateToStatus(string sqlState, int expectedStatus, string expectedType)
    {
        var mapping = ExceptionMapper.Map(Postgres(sqlState), requestAborted: false);

        Assert.Equal(expectedStatus, mapping.StatusCode);
        Assert.Equal(expectedType, mapping.Type);
        Assert.Equal(sqlState, mapping.SqlState);
    }

    [Theory]
    [MemberData(nameof(SqlStateRows))]
    public void Map_DbUpdateExceptionWrappingPostgresException_UnwrapsAndMapsTheSame(
        string sqlState, int expectedStatus, string expectedType)
    {
        var exception = new DbUpdateException("An error occurred while saving the entity changes.", Postgres(sqlState));

        var mapping = ExceptionMapper.Map(exception, requestAborted: false);

        Assert.Equal(expectedStatus, mapping.StatusCode);
        Assert.Equal(expectedType, mapping.Type);
    }

    [Fact]
    public void Map_PostgresException_DoesNotExposeDatabaseMessage()
    {
        var exception = Postgres(PostgresErrorCodes.UniqueViolation,
            "duplicate key value violates unique constraint \"ux_users_normalized_email\"");

        var mapping = ExceptionMapper.Map(exception, requestAborted: false);

        Assert.Null(mapping.Detail);
        Assert.DoesNotContain("ux_users", mapping.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_InsufficientPrivilege_IsLoggedAsError()
    {
        var mapping = ExceptionMapper.Map(Postgres(PostgresErrorCodes.InsufficientPrivilege), requestAborted: false);

        Assert.Equal(LogLevel.Error, mapping.LogLevel);
        Assert.Null(mapping.Detail);
    }

    [Fact]
    public void Map_UnknownException_Returns500WithGenericMessage()
    {
        var mapping = ExceptionMapper.Map(new InvalidOperationException("Sensitive internal detail"), requestAborted: false);

        AssertMapping(mapping, StatusCodes.Status500InternalServerError, ProblemTypes.Internal, LogLevel.Error);
        Assert.Null(mapping.Detail);
        Assert.Equal(ExceptionMapper.InternalTitle, mapping.Title);
    }

    [Fact]
    public void Map_OperationCanceledWhenRequestAborted_Returns499WithoutBody()
    {
        var mapping = ExceptionMapper.Map(new OperationCanceledException(), requestAborted: true);

        Assert.Equal(ExceptionMapper.ClientClosedRequest, mapping.StatusCode);
        Assert.False(mapping.WriteBody);
        Assert.Equal(LogLevel.Debug, mapping.LogLevel);
    }

    [Fact]
    public void Map_OperationCanceledWithoutRequestAbort_Returns500()
    {
        var mapping = ExceptionMapper.Map(new TaskCanceledException(), requestAborted: false);

        Assert.Equal(StatusCodes.Status500InternalServerError, mapping.StatusCode);
        Assert.True(mapping.WriteBody);
    }

    [Fact]
    public void Map_BadHttpRequestException_ReturnsItsStatus()
    {
        var mapping = ExceptionMapper.Map(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge), requestAborted: false);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, mapping.StatusCode);
        Assert.Equal(ProblemTypes.BadRequest, mapping.Type);
    }

    [Fact]
    public void FindPostgresException_DeeplyNested_ReturnsIt()
    {
        var postgres = Postgres(PostgresErrorCodes.CheckViolation);
        var exception = new InvalidOperationException("outer", new AggregateException("middle", postgres));

        Assert.Same(postgres, ExceptionMapper.FindPostgresException(exception));
    }

    private static PostgresException Postgres(string sqlState, string message = "database error") =>
        new(message, "ERROR", "ERROR", sqlState);

    private static void AssertMapping(ProblemMapping mapping, int status, string type, LogLevel level)
    {
        Assert.Equal(status, mapping.StatusCode);
        Assert.Equal(type, mapping.Type);
        Assert.Equal(level, mapping.LogLevel);
        Assert.True(mapping.WriteBody);
    }
}
