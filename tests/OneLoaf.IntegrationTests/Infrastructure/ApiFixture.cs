using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneLoaf.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace OneLoaf.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 16 container per test run, initialized exactly like production: <c>db/security/docker-init.sh</c>
/// runs <c>bootstrap.sql</c> (roles, database), then the EF Core migrations are applied as <c>app_admin</c>. The API
/// connects as <c>oneloaf_app</c>. Test RSA keys are generated per run; nothing secret is stored.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string Database = "oneloaf";
    public const string Issuer = "https://localhost/core";
    public const string Audience = "oneloaf";

    private const string AdminPassword = "admin-test-password";
    private const string AppPassword = "app-test-password";
    private const string ReaderPassword = "reader-test-password";
    private const string MigrationsHistoryTable = "__ef_migrations_history";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly PostgreSqlContainer _postgres;
    private readonly List<CoreApiFactory> _factories = [];

    public ApiFixture()
    {
        var root = FindRepositoryRoot();
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithEnvironment("ONELOAF_DB", Database)
            .WithEnvironment("ONELOAF_ADMIN_PASSWORD", AdminPassword)
            .WithEnvironment("ONELOAF_APP_PASSWORD", AppPassword)
            .WithEnvironment("ONELOAF_READER_PASSWORD", ReaderPassword)
            .WithResourceMapping(FilePath.Of(Path.Combine(root, "db", "security", "bootstrap.sql")),
                DirectoryPath.Of("/oneloaf/"))
            .WithResourceMapping(FilePath.Of(Path.Combine(root, "db", "security", "docker-init.sh")),
                DirectoryPath.Of("/docker-entrypoint-initdb.d/"))
            .Build();
    }

    /// <summary>The API with a high login rate limit, shared by most tests.</summary>
    public CoreApiFactory Api { get; private set; } = null!;

    /// <summary>Connection string of the application role (what the services use).</summary>
    public string AppConnectionString => ConnectionString("oneloaf_app", AppPassword);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OneLoafDbContext>()
            .UseNpgsql(ConnectionString("app_admin", AdminPassword),
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .Options;
        await using (var db = new OneLoafDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        Api = CreateApi(authPermitLimit: 10_000);
    }

    /// <summary>Creates another API instance, for example with a low rate limit.</summary>
    public CoreApiFactory CreateApi(int authPermitLimit)
    {
        var factory = new CoreApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:OneLoaf"] = AppConnectionString,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:PublicKeyPem"] = _rsa.ExportSubjectPublicKeyInfoPem(),
            ["Jwt:SigningKeyPem"] = _rsa.ExportRSAPrivateKeyPem(),
            ["RateLimiting:Auth:PermitLimit"] = authPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Serilog:MinimumLevel:Default"] = "Warning",
        });
        _factories.Add(factory);
        return factory;
    }

    /// <summary>Opens a connection as <c>oneloaf_app</c> for assertions directly in the database.</summary>
    public async Task<NpgsqlConnection> OpenAppConnectionAsync()
    {
        var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        _rsa.Dispose();
    }

    private string ConnectionString(string username, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = _postgres.Hostname,
            Port = _postgres.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort),
            Database = Database,
            Username = username,
            Password = password,
        }.ConnectionString;

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OneLoaf.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (OneLoaf.slnx) not found.");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
