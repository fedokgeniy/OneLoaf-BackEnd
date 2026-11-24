using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Infrastructure.Persistence;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

/// <summary>Registration of the cache.</summary>
public static class CachingExtensions
{
    /// <summary>
    /// HybridCache (in-process L1) with Redis as the shared L2 when <c>Redis__ConnectionString</c> is set, and the
    /// cached decorators of the reference data and the calculations.
    /// </summary>
    public static IServiceCollection AddCoreApiCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()?.ConnectionString;
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = CacheKeys.InstanceName;
            });
        }

        services.AddHybridCache();
        services.AddSingleton<CacheMetrics>();
        services.AddScoped<ReferenceDataReader>();
        services.AddScoped<IReferenceDataReader, CachedReferenceDataReader>();
        services.AddScoped<RecipeCalculations>();
        services.AddScoped<IRecipeCalculations, CachedRecipeCalculations>();
        return services;
    }

    /// <summary>After a successful write request (not GET/HEAD/OPTIONS) removes the caller's cached calculations.</summary>
    public static IApplicationBuilder UseCalculationCacheInvalidation(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            await next(context);
            var method = context.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
                || context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            {
                return;
            }

            var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
            if (currentUser.IsAuthenticated)
            {
                var cache = context.RequestServices.GetRequiredService<HybridCache>();
                await cache.RemoveByTagAsync(CacheKeys.UserCalculationsTag(currentUser.Id), context.RequestAborted);
            }
        });
}
