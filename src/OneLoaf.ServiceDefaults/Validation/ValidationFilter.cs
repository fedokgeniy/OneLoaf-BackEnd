using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Errors;

namespace OneLoaf.ServiceDefaults.Validation;

/// <summary>
/// Endpoint filter that validates the <typeparamref name="TRequest"/> argument with its FluentValidation validator and
/// answers 400 <c>ValidationProblemDetails</c> before the handler runs (CLAUDE.md rule 8).
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();
        if (request is null)
        {
            return ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [string.Empty] = ["A request body is required."],
            });
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return result.IsValid
            ? await next(context)
            : ValidationProblem(ExceptionMapper.ToErrors(result.Errors));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ValidationProblem ValidationProblem(
        IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: ExceptionMapper.ValidationTitle, type: ProblemTypes.Validation);
}

/// <summary>Registration helper for <see cref="ValidationFilter{TRequest}"/>.</summary>
public static class ValidationFilterExtensions
{
    /// <summary>Validates the request body of type <typeparamref name="TRequest"/> and documents the 400 response.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
}
