using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneLoaf.Infrastructure.Persistence;

// Applies all EF Core migrations to the database given by ConnectionStrings:OneLoaf.
// Sources (later wins): user-secrets (local development) -> environment variables.
//   dotnet user-secrets set "ConnectionStrings:OneLoaf" "Host=...;Password=..." --project src/OneLoaf.DbMigrator
//   or: export ConnectionStrings__OneLoaf="Host=...;Password=..."

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = config.GetConnectionString("OneLoaf");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Connection string 'ConnectionStrings:OneLoaf' is not set (env ConnectionStrings__OneLoaf or user-secrets).");
    return 1;
}

var options = new DbContextOptionsBuilder<OneLoafDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
    .Options;

await using var db = new OneLoafDbContext(options);

var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
Console.WriteLine(pending.Count == 0
    ? "Database is up to date."
    : $"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");

await db.Database.MigrateAsync();

foreach (var m in await db.Database.GetAppliedMigrationsAsync())
    Console.WriteLine($"  applied: {m}");

return 0;
