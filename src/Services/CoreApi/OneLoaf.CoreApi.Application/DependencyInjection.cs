using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;

namespace OneLoaf.CoreApi.Application;

/// <summary>Registration of the Core API use-case services and validators.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the Application layer.</summary>
    public static IServiceCollection AddCoreApiApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
