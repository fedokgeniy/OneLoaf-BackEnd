namespace OneLoaf.CoreApi.Application.Common;

/// <summary>Runs several repository calls of one request as one database transaction: all of them are stored or none.</summary>
public interface IWorkspaceTransaction
{
    /// <summary>Runs <paramref name="work"/> in a transaction and commits when it returns; an exception rolls it back.</summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
