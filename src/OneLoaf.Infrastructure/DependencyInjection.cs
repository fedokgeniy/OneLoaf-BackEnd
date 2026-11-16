using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the OneLoaf DbContext for PostgreSQL. Used by the services (Core API etc.).</summary>
    public static IServiceCollection AddOneLoafDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OneLoafDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));
        return services;
    }
}
