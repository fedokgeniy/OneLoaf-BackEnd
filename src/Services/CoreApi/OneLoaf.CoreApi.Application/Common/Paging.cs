using FluentValidation;
using FluentValidation.Results;

namespace OneLoaf.CoreApi.Application.Common;

/// <summary>One page of a list: <c>{ items, page, pageSize, totalCount }</c>.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items of the page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="TotalCount">Number of items over all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Paging limits of list endpoints.</summary>
public static class Paging
{
    /// <summary>Default page size.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Largest allowed page size.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Number of rows to skip for a page.</summary>
    public static int Skip(int page, int pageSize) => (Math.Max(page, 1) - 1) * pageSize;

    /// <summary>Adds the page and page size rules to a validator.</summary>
    public static void AddPagingRules<T>(this AbstractValidator<T> validator, Func<T, int> page, Func<T, int> pageSize)
    {
        ArgumentNullException.ThrowIfNull(validator);
        validator.RuleFor(x => page(x)).GreaterThanOrEqualTo(1).OverridePropertyName("page");
        validator.RuleFor(x => pageSize(x)).InclusiveBetween(1, MaxPageSize).OverridePropertyName("pageSize");
    }
}

/// <summary>Raises a 400 validation error for a single field from service code (checks that need the database).</summary>
public static class ValidationFailures
{
    /// <summary>Throws <see cref="ValidationException"/> with one error.</summary>
    public static ValidationException For(string propertyName, string message) =>
        new([new ValidationFailure(propertyName, message)]);
}
