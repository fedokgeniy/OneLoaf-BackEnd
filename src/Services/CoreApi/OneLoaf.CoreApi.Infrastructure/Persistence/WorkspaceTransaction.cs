using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IWorkspaceTransaction"/> on the request's database context.</summary>
public sealed class WorkspaceTransaction(OneLoafDbContext db) : IWorkspaceTransaction
{
    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
