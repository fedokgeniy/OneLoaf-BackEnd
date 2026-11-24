using Microsoft.EntityFrameworkCore;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Notification.Application;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>Recipients from <c>users</c> and <c>kitchen_settings</c>.</summary>
public sealed class RecipientDirectory(OneLoafDbContext db) : IRecipientDirectory
{
    private const string DefaultTimeZone = "Europe/Vilnius";

    /// <inheritdoc />
    public Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new Recipient(u.Id, u.Email, u.KitchenSettings != null ? u.KitchenSettings.TimeZone : DefaultTimeZone))
            .FirstOrDefaultAsync(cancellationToken);
}
