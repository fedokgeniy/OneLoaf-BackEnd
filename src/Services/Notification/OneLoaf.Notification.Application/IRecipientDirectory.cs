namespace OneLoaf.Notification.Application;

/// <summary>Recipients by user id.</summary>
public interface IRecipientDirectory
{
    /// <summary>The recipient, or null when the account no longer exists.</summary>
    Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
