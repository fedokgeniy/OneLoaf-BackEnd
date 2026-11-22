using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.Infrastructure.Auth;
using OneLoaf.CoreApi.Infrastructure.Persistence;
using OneLoaf.Infrastructure;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Options;

namespace OneLoaf.CoreApi.Infrastructure;

/// <summary>Registration of the Core API infrastructure. Called only from <c>Program.cs</c>.</summary>
public static class DependencyInjection
{
    /// <summary>Name of the connection string (<c>ConnectionStrings__OneLoaf</c>, login <c>oneloaf_app</c>).</summary>
    public const string ConnectionStringName = "OneLoaf";

    /// <summary>Name of the database readiness check.</summary>
    public const string DatabaseHealthCheckName = "postgres";

    /// <summary>Registers the database context, repositories, password hashing, token issuing and the readiness check.</summary>
    public static IServiceCollection AddCoreApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not set (env ConnectionStrings__{ConnectionStringName} or user-secrets).");
        }

        services.AddOneLoafDatabase(connectionString);
        services.AddHealthChecks()
            .AddDbContextCheck<OneLoafDbContext>(DatabaseHealthCheckName, tags: [HealthTags.Ready]);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddValidatedOptions<JwtIssuerOptions>(JwtIssuerOptions.SectionName);
        services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();
        return services;
    }
}
