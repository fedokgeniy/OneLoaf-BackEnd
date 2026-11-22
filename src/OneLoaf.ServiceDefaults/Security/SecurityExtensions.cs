using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneLoaf.ServiceDefaults.Options;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.ServiceDefaults.Security;

/// <summary>Authentication and authorization setup shared by all services (architecture.md §4).</summary>
public static class SecurityExtensions
{
    /// <summary>
    /// JWT bearer validation with the public key, fallback policy "authenticated user", <see cref="Policies.Admin"/>,
    /// and <see cref="ICurrentUser"/>. Anonymous endpoints opt out with <c>AllowAnonymous()</c>.
    /// </summary>
    public static IServiceCollection AddOneLoafSecurity(this IServiceCollection services)
    {
        services.AddValidatedOptions<JwtOptions>(JwtOptions.SectionName);
        services.AddSingleton<JwtValidationKey>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
