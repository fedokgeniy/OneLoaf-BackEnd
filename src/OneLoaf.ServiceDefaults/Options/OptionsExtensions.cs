using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OneLoaf.ServiceDefaults.Options;

/// <summary>Options pattern helper: configuration comes only from environment / user-secrets and fails fast.</summary>
public static class OptionsExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to the configuration section, validates its data annotations and
    /// validates on start, so a missing variable stops the service instead of failing on the first request.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(this IServiceCollection services, string sectionName)
        where TOptions : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return services.AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
