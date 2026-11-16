using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OneLoaf.Infrastructure.Persistence;

/// <summary>
/// Used by the dotnet-ef tool (migrations add / script / database update).
/// The connection string comes from the ConnectionStrings__OneLoaf environment variable;
/// no credentials are stored in the repository. Generating migrations and SQL scripts
/// does not open a connection, so the fallback below has no password.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OneLoafDbContext>
{
    public OneLoafDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OneLoaf")
                               ?? "Host=localhost;Port=5432;Database=oneloaf";

        var options = new DbContextOptionsBuilder<OneLoafDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new OneLoafDbContext(options);
    }
}
