using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Library;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.CoreApi.Infrastructure.Auth;
using OneLoaf.CoreApi.Infrastructure.Caching;
using OneLoaf.CoreApi.Infrastructure.Persistence;
using OneLoaf.Infrastructure;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Messaging.RabbitMq;
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

    /// <summary>Registers the database context, repositories, password hashing, token issuing, the event publisher and the readiness checks.</summary>
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
        services.AddScoped<IKitchenSettingsRepository, KitchenSettingsRepository>();
        services.AddScoped<IFlourRepository, FlourRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IWorkspaceTransaction, WorkspaceTransaction>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddCoreApiCaching(configuration);
        services.AddScoped<IWarningsReader, WarningsReader>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IStarterRepository, StarterRepository>();
        services.AddScoped<IBakeRecordRepository, BakeRecordRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<IBakeRecordPhotoRepository, BakeRecordPhotoRepository>();
        services.AddScoped<IPhotoSlotRepository, PhotoSlotRepository>();
        services.AddValidatedOptions<PhotoStorageOptions>(PhotoStorageOptions.SectionName);
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        services.AddScoped<Application.Export.IExportReader, ExportReader>();
        services.AddRabbitMqPublisher();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddValidatedOptions<JwtIssuerOptions>(JwtIssuerOptions.SectionName);
        services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton(provider => new SessionOptions(TimeSpan.FromDays(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtIssuerOptions>>().Value.RefreshTokenDays)));
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        return services;
    }
}
