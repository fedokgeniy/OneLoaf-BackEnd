namespace OneLoaf.Assistant.Application.Chat;

/// <summary>Proposals waiting for confirmation (Redis with expiry).</summary>
public interface IPendingActionStore
{
    /// <summary>Stores a proposal.</summary>
    Task SaveAsync(PendingAction action, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>The proposal, or null when unknown or expired.</summary>
    Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken);

    /// <summary>Removes a proposal; false when it was already gone (used or expired).</summary>
    Task<bool> DeleteAsync(Guid actionId, CancellationToken cancellationToken);
}
