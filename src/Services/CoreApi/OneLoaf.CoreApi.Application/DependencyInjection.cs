using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Application.Warnings;

namespace OneLoaf.CoreApi.Application;

/// <summary>Registration of the Core API use-case services and validators.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the Application layer.</summary>
    public static IServiceCollection AddCoreApiApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<ReferenceGuard>();
        services.AddScoped<KitchenSettingsService>();
        services.AddScoped<FlourService>();
        services.AddScoped<IngredientService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<Library.LibraryService>();
        services.AddScoped<Library.ModerationService>();
        services.AddScoped<Calculations.OvenLoadingService>();
        services.AddScoped<Export.ExportService>();
        services.AddScoped<CalculationService>();
        services.AddScoped<WarningsService>();
        services.AddScoped<BatchService>();
        services.AddScoped<StarterService>();
        services.AddScoped<BakeRecordService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
