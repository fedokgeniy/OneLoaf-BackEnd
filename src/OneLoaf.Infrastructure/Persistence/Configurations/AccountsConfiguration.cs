using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_email_format", "email ~ '^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$'");
            t.HasCheckConstraint("ck_users_normalized_email", "normalized_email = upper(email)");
            t.HasCheckConstraint("ck_users_display_name_not_blank", "length(btrim(display_name)) > 0");
            t.HasComment("E1. Registered bakers and administrators; each user owns a private workspace.");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.NormalizedEmail).IsUnique();

        b.HasOne(x => x.KitchenSettings).WithOne(x => x.User)
            .HasForeignKey<KitchenSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class KitchenSettingsConfiguration : IEntityTypeConfiguration<KitchenSettings>
{
    public void Configure(EntityTypeBuilder<KitchenSettings> b)
    {
        b.ToTable("kitchen_settings", t =>
        {
            t.HasComment("E3, US-13, US-47-49. Equipment and cost parameters, one row per user.");
            t.HasCheckConstraint("ck_kitchen_settings_oven_power_kw", "oven_power_kw > 0");
            t.HasCheckConstraint("ck_kitchen_settings_oven_capacity_loaves", "oven_capacity_loaves > 0");
            t.HasCheckConstraint("ck_kitchen_settings_mixer_bowl_capacity_kg", "mixer_bowl_capacity_kg > 0");
            t.HasCheckConstraint("ck_kitchen_settings_electricity_tariff_per_kwh", "electricity_tariff_per_kwh >= 0");
            t.HasCheckConstraint("ck_kitchen_settings_packaging_cost_per_loaf", "packaging_cost_per_loaf >= 0");
            t.HasCheckConstraint("ck_kitchen_settings_loss_rate_percent", "loss_rate_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_kitchen_settings_labor_rate_per_hour", "labor_rate_per_hour >= 0");
        });
        b.HasKey(x => x.UserId);
        b.Property(x => x.OvenPowerKw).HasPrecision(6, 2);
        b.Property(x => x.MixerBowlCapacityKg).HasPrecision(6, 2);
        b.Property(x => x.ElectricityTariffPerKwh).HasPrecision(8, 4);
        b.Property(x => x.PackagingCostPerLoaf).HasPrecision(10, 4);
        b.Property(x => x.LossRatePercent).HasPrecision(5, 2);
        b.Property(x => x.LaborRatePerHour).HasPrecision(10, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(x => x.TimeZone).HasMaxLength(64);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.NoAction);
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.ToTable("push_subscriptions", t => t.HasComment("Web Push subscriptions of users' devices (Notification service)."));
        b.HasKey(x => x.Id);
        b.Property(x => x.Endpoint).HasMaxLength(1024).IsRequired();
        b.Property(x => x.P256dh).HasMaxLength(256).IsRequired();
        b.Property(x => x.Auth).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.Endpoint).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
