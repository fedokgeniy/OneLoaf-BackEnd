namespace OneLoaf.Domain.Entities;

/// <summary>E1. Registered baker (or administrator). Owner of a private workspace.</summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    /// <summary>Salted hash produced by PasswordHasher (PBKDF2). Never plain text (NFR-04).</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public KitchenSettings? KitchenSettings { get; set; }
}

/// <summary>E3, US-13, US-47 - US-49. One row per user: equipment and cost parameters.</summary>
public class KitchenSettings
{
    public Guid UserId { get; set; }
    public decimal? OvenPowerKw { get; set; }
    public int? OvenCapacityLoaves { get; set; }
    public decimal? MixerBowlCapacityKg { get; set; }
    public decimal? ElectricityTariffPerKwh { get; set; }
    public decimal PackagingCostPerLoaf { get; set; }
    public decimal LossRatePercent { get; set; }
    public decimal? LaborRatePerHour { get; set; }
    public string Currency { get; set; } = "EUR";
    public string TimeZone { get; set; } = "Europe/Vilnius";
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>Web Push subscription of a user's device (Notification service).</summary>
public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// E1, US-02. A refresh token of a signed-in browser. Only the hash of the value is stored. Tokens of one sign-in form
/// a family: each exchange marks the token as rotated and adds the next one; an exchanged token that is used again
/// revokes the whole family.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    /// <summary>SHA-256 of the token value, hex.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
