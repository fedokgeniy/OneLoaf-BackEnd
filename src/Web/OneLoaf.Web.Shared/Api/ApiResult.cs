using System.Diagnostics.CodeAnalysis;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Result of an API call with a body: <see cref="Success"/> or <see cref="Failure"/>. Never thrown.</summary>
/// <typeparam name="T">Response body type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory methods keep call sites short: ApiResult<T>.Ok(value).")]
public abstract record ApiResult<T>
{
    private ApiResult()
    {
    }

    /// <summary>The call succeeded with <paramref name="Value"/>.</summary>
    public sealed record Success(T Value) : ApiResult<T>;

    /// <summary>The call failed with <paramref name="Error"/>.</summary>
    public sealed record Failure(ApiError Error) : ApiResult<T>;

    /// <summary>Creates a success.</summary>
    public static ApiResult<T> Ok(T value) => new Success(value);

    /// <summary>Creates a failure.</summary>
    public static ApiResult<T> Fail(ApiError error) => new Failure(error);
}

/// <summary>Result of an API call without a body (204): <see cref="Success"/> or <see cref="Failure"/>.</summary>
public abstract record ApiResult
{
    private ApiResult()
    {
    }

    /// <summary>The single success value.</summary>
    public static ApiResult Ok { get; } = new Success();

    /// <summary>The call succeeded.</summary>
    public sealed record Success : ApiResult;

    /// <summary>The call failed with <paramref name="Error"/>.</summary>
    public sealed record Failure(ApiError Error) : ApiResult;

    /// <summary>Creates a failure.</summary>
    public static ApiResult Fail(ApiError error) => new Failure(error);
}
