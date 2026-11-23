using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Assistant.Application.Analysis;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Tools;

namespace OneLoaf.Assistant.Application;

/// <summary>Registration of the Assistant use cases and tools.</summary>
public static class DependencyInjection
{
    /// <summary>Registers services, the tools and the validators.</summary>
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();
        services.AddScoped<AnalysisService>();
        services.AddScoped<UsageGuard>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<IAssistantTool, FindRecipesTool>();
        services.AddScoped<IAssistantTool, GetRecipeTool>();
        services.AddScoped<IAssistantTool, CalculateCostTool>();
        services.AddScoped<IAssistantTool, CalculateTimelineTool>();
        services.AddScoped<IAssistantTool, CalculateStartWindowTool>();
        services.AddScoped<IAssistantTool, CalculateProductionFiguresTool>();
        services.AddScoped<IAssistantTool, ListStartersTool>();
        services.AddScoped<IAssistantTool, PreviewBatchPlanTool>();
        services.AddScoped<IAssistantTool, ProposeBatchTool>();
        services.AddScoped<IAssistantTool, GetBakeAnalysisDataTool>();
        services.AddScoped<IAssistantTool, GetIngredientPricesTool>();
        services.AddScoped<IAssistantTool, GetMissingDataTool>();
        services.AddValidatorsFromAssemblyContaining<SendMessageRequestValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
