namespace OneLoaf.Web.Shared.Contracts;

/// <summary>One page of a list: <c>{ items, page, pageSize, totalCount }</c> (docs/api/guides/pagination.md).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>More items exist after this page.</summary>
    public bool HasMore => Page * PageSize < TotalCount;
}
